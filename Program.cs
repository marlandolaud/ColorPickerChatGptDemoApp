using ColorPickerApp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddConsole(opts => opts.FormatterName = "simple");
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

var tenantId            = builder.Configuration["AZURE_TENANT_ID"]
    ?? throw new InvalidOperationException("AZURE_TENANT_ID is required");
var audience            = builder.Configuration["MCP_API_CLIENT_ID"]
    ?? throw new InvalidOperationException("MCP_API_CLIENT_ID is required");
var mcpPublicUrl        = builder.Configuration["MCP_PUBLIC_URL"]
    ?? throw new InvalidOperationException("MCP_PUBLIC_URL is required");
var chatgptClientId     = builder.Configuration["CHATGPT_CLIENT_ID"]
    ?? throw new InvalidOperationException("CHATGPT_CLIENT_ID is required");
var chatgptClientSecret = builder.Configuration["CHATGPT_CLIENT_SECRET"]
    ?? throw new InvalidOperationException("CHATGPT_CLIENT_SECRET is required");
var proxySecret         = builder.Configuration["OAUTH_PROXY_SECRET"]
    ?? throw new InvalidOperationException("OAUTH_PROXY_SECRET is required");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        options.Audience  = audience;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.Response.Headers.WWWAuthenticate =
                    $"Bearer resource_metadata=\"{mcpPublicUrl}/.well-known/oauth-protected-resource\"";
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

var app = builder.Build();
app.UseHttpLogging();

app.Use(async (ctx, next) =>
{
    var log = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    log.LogInformation("[REQ] {Method} {Path}{Query} | Origin={Origin} | Accept={Accept}",
        ctx.Request.Method,
        ctx.Request.Path,
        ctx.Request.QueryString,
        ctx.Request.Headers["Origin"].ToString(),
        ctx.Request.Headers["Accept"].ToString());

    ctx.Response.OnStarting(() =>
    {
        log.LogInformation("[RES] {Method} {Path} -> {Status}",
            ctx.Request.Method,
            ctx.Request.Path,
            ctx.Response.StatusCode);
        return Task.CompletedTask;
    });

    await next(ctx);
});

app.UseAuthentication();
app.UseAuthorization();

// RFC 9728 — points ChatGPT to our synthetic AS
app.MapGet("/.well-known/oauth-protected-resource", () => Results.Json(new
{
    resource                 = mcpPublicUrl,
    authorization_servers    = new[] { mcpPublicUrl },
    scopes_supported         = new[] { "mcp.access" },
    bearer_methods_supported = new[] { "header" }
}));

// RFC 8414 — synthetic AS metadata; advertises CIMD support so ChatGPT enables the flow
app.MapGet("/.well-known/oauth-authorization-server", () => Results.Json(new
{
    issuer                                    = mcpPublicUrl,
    authorization_endpoint                    = $"{mcpPublicUrl}/oauth/authorize",
    token_endpoint                            = $"{mcpPublicUrl}/oauth/token",
    scopes_supported                          = new[] { "mcp.access" },
    response_types_supported                  = new[] { "code" },
    grant_types_supported                     = new[] { "authorization_code" },
    code_challenge_methods_supported          = new[] { "S256" },
    token_endpoint_auth_methods_supported     = new[] { "none" },
    client_id_metadata_document_supported     = true
}));

// CIMD authorize — accepts ChatGPT's metadata URL as client_id, proxies to Azure AD
// ChatGPT is treated as a public client; the client_secret stays server-side.
app.MapGet("/oauth/authorize", (HttpRequest req) =>
{
    var redirectUri   = req.Query["redirect_uri"].ToString();
    var state         = req.Query["state"].ToString();
    var codeChallenge = req.Query["code_challenge"].ToString();

    // Encode ChatGPT's redirect_uri, state, and PKCE challenge into signed Azure AD state
    var packed = ProxyPack(new Dictionary<string, string>
    {
        ["rd"] = redirectUri,
        ["st"] = state,
        ["cc"] = codeChallenge
    }, proxySecret);

    var qs = string.Join("&", new[]
    {
        ("response_type",  "code"),
        ("response_mode",  "form_post"),          // avoids URL-length limit on APIM callback
        ("client_id",      chatgptClientId),
        ("redirect_uri",   $"{mcpPublicUrl}/oauth/callback"),
        ("scope",          $"api://{tenantId}/mcp-color-picker/mcp.access"),
        ("state",          packed)
    }.Select(kv => $"{Uri.EscapeDataString(kv.Item1)}={Uri.EscapeDataString(kv.Item2)}"));

    return Results.Redirect(
        $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize?{qs}");
});

// Azure AD posts the auth code here (form_post mode); extract state, re-sign, redirect to ChatGPT
app.MapPost("/oauth/callback", async (HttpRequest req) =>
{
    var form  = await req.ReadFormAsync();
    var code  = form["code"].ToString();
    var state = form["state"].ToString();

    var ps = ProxyUnpack(state, proxySecret);
    if (ps == null) return Results.BadRequest("invalid proxy state");

    // Sign the Azure AD code and include the PKCE challenge for validation at /oauth/token
    var chatCode = ProxyPack(new Dictionary<string, string>
    {
        ["az"] = code,
        ["cc"] = ps["cc"]
    }, proxySecret);

    var sep = ps["rd"].Contains('?') ? "&" : "?";
    return Results.Redirect(
        $"{ps["rd"]}{sep}code={Uri.EscapeDataString(chatCode)}&state={Uri.EscapeDataString(ps["st"])}");
});

// ChatGPT sends code + PKCE verifier here; we validate, then exchange with Azure AD
app.MapPost("/oauth/token", async (HttpRequest req, IHttpClientFactory factory) =>
{
    var form     = await req.ReadFormAsync();
    var code     = form["code"].ToString();
    var verifier = form["code_verifier"].ToString();

    var pc = ProxyUnpack(code, proxySecret);
    if (pc == null) return Results.Unauthorized();

    // Validate PKCE: S256(verifier) must match the code_challenge from /oauth/authorize
    var hash     = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
    var computed = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    if (computed != pc["cc"]) return Results.Unauthorized();

    using var http = factory.CreateClient();
    var tokenResp = await http.PostAsync(
        $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["client_id"]     = chatgptClientId,
            ["client_secret"] = chatgptClientSecret,
            ["code"]          = pc["az"],
            ["redirect_uri"]  = $"{mcpPublicUrl}/oauth/callback",
            ["scope"]         = $"api://{tenantId}/mcp-color-picker/mcp.access"
        }));

    var json = await tokenResp.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json", statusCode: (int)tokenResp.StatusCode);
});

app.MapMcp("/mcp").RequireAuthorization();
app.Run();

// --- HMAC-signed state/code packing (stateless across load-balanced instances) ---

static string ProxyPack(Dictionary<string, string> data, string secret)
{
    var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)))
                         .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                     .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    return $"{payload}.{sig}";
}

static Dictionary<string, string>? ProxyUnpack(string token, string secret)
{
    var dot = token.LastIndexOf('.');
    if (dot < 0) return null;
    var payload = token[..dot];
    var sig     = token[(dot + 1)..];
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                          .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    if (sig != expected) return null;
    try
    {
        var padded = payload.Replace('-', '+').Replace('_', '/')
                            .PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        return JsonSerializer.Deserialize<Dictionary<string, string>>(
            Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
    }
    catch { return null; }
}
