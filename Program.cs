using ColorPickerApp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;

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

var tenantId     = builder.Configuration["AZURE_TENANT_ID"]
    ?? throw new InvalidOperationException("AZURE_TENANT_ID is required");
var audience     = builder.Configuration["MCP_API_CLIENT_ID"]
    ?? throw new InvalidOperationException("MCP_API_CLIENT_ID is required");
var mcpPublicUrl = builder.Configuration["MCP_PUBLIC_URL"]
    ?? throw new InvalidOperationException("MCP_PUBLIC_URL is required");

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

// RFC 9728 — auto-discovery so ChatGPT knows where to get tokens (CIMD flow)
app.MapGet("/.well-known/oauth-protected-resource", () => Results.Json(new
{
    resource                 = mcpPublicUrl,
    authorization_servers    = new[] { $"https://login.microsoftonline.com/{tenantId}/v2.0" },
    scopes_supported         = new[] { "mcp.access" },
    bearer_methods_supported = new[] { "header" }
}));

app.MapMcp("/mcp").RequireAuthorization();
app.Run();
