using Serilog;
using SqlCmdr.Extensions;

var builder = WebApplication.CreateBuilder(args);

var fileLoggingEnabled = string.Equals(builder.Configuration["SQLCMDR_FILE_LOG"], "1", StringComparison.OrdinalIgnoreCase);

var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console();

if (fileLoggingEnabled)
{
    loggerConfig = loggerConfig.WriteTo.File("logs/sqlcmdr-.txt", rollingInterval: RollingInterval.Day);
}

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddRazorPages();
builder.Services.AddSqlCmdr();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.UseSerilogRequestLogging();

Log.Information("SQL Cmdr starting...");

app.Run();

