# Program.cs — Curity Identity Server variant

With Curity as the IDP the synthetic AS proxy is gone entirely. Curity natively supports CIMD ([RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700)) and dynamic client registration, so `authorization_servers` in the protected resource metadata points directly at Curity. The MCP server no longer needs to expose `/.well-known/oauth-authorization-server`, `/oauth/authorize`, `/oauth/callback`, or `/oauth/token`.

## What changes vs the Azure Entra ID version

| Removed | Why |
|---|---|
| `AZURE_TENANT_ID` | Curity has no tenant concept |
| `CHATGPT_CLIENT_ID` / `CHATGPT_CLIENT_SECRET` | No pre-registered confidential client — Curity resolves ChatGPT's CIMD URL on the fly |
| `OAUTH_PROXY_SECRET` | No HMAC-signed state packing — proxy endpoints are gone |
| `/.well-known/oauth-authorization-server` | Curity publishes its own RFC 8414 metadata |
| `/oauth/authorize`, `/oauth/callback`, `/oauth/token` | Curity handles the full authorization code flow |
| `ProxyPack` / `ProxyUnpack` helpers | Only existed to support the proxy |
| `AddHttpClient()` | No outbound calls to a real IDP at runtime |

| Added | Why |
|---|---|
| `CURITY_ISSUER_URL` | Curity runtime base URL — used as JWT authority and AS pointer |
| `MCP_AUDIENCE` | Audience claim Curity stamps on issued JWTs |

## Environment variables

```
CURITY_ISSUER_URL=https://idsvr:8443/oauth/v2/oauth-anonymous
MCP_AUDIENCE=mcp-color-picker
MCP_PUBLIC_URL=https://<apim-or-ngrok-host>/   # no trailing slash
```

`CURITY_ISSUER_URL` is the Curity runtime issuer — find it in **Admin UI → Profiles → \<profile\> → General → Issuer**.  
`MCP_AUDIENCE` matches the **Audience** field on the API you create in Curity for the MCP server.

## Example Program.cs

```csharp
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

var curityIssuerUrl = builder.Configuration["CURITY_ISSUER_URL"]
    ?? throw new InvalidOperationException("CURITY_ISSUER_URL is required");
var audience        = builder.Configuration["MCP_AUDIENCE"]
    ?? throw new InvalidOperationException("MCP_AUDIENCE is required");
var mcpPublicUrl    = builder.Configuration["MCP_PUBLIC_URL"]
    ?? throw new InvalidOperationException("MCP_PUBLIC_URL is required");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = curityIssuerUrl;
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

// RFC 9728 — points ChatGPT directly at Curity; no synthetic AS needed
app.MapGet("/.well-known/oauth-protected-resource", () => Results.Json(new
{
    resource                 = mcpPublicUrl,
    authorization_servers    = new[] { curityIssuerUrl },
    scopes_supported         = new[] { "mcp.access" },
    bearer_methods_supported = new[] { "header" }
}));

app.MapMcp("/mcp").RequireAuthorization();
app.Run();
```

## Curity configuration checklist

1. **OAuth profile** — create a Code Flow profile in Admin UI.
2. **API** — add an API resource with audience `mcp-color-picker` and scope `mcp.access`.
3. **CIMD** — enable *Client ID Metadata Documents* on the token service profile (Admin UI → Token Service → General → Client Registration).
4. **Allowed metadata URL pattern** — add `https://chatgpt.com/*` to the trusted CIMD URL list so Curity resolves ChatGPT's client metadata document.
5. **Signing key** — ensure the profile has an active RS256 or ES256 signing key; the MCP server discovers it via JWKS automatically.

## Reference

| Resource | URL |
|---|---|
| Curity — Implement MCP Authorization | https://curity.io/resources/learn/implementing-mcp-authorization-apis/ |
| Curity — OAuth Client ID Metadata Document (CIMD) | https://curity.io/resources/learn/oauth-client-id-metadata-document/ |
| RFC 9700 — OAuth 2.0 Client ID Metadata Document | https://datatracker.ietf.org/doc/html/rfc9700 |
| RFC 9728 — OAuth 2.0 Protected Resource Metadata | https://datatracker.ietf.org/doc/html/rfc9728 |
