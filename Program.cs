using ColorPickerApp;
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

var app = builder.Build();
app.UseHttpLogging();

// Log every request/response at a glance
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

app.MapMcp("/mcp");
app.Run();
