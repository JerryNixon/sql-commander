using Serilog;
using SqlCmdr.Library.Services;
using SqlCmdr.Library.Abstractions;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/sqlcmdr-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddRazorPages();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IMetadataService, MetadataService>();
builder.Services.AddSingleton<IQueryExecutionService, QueryExecutionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.UseSerilogRequestLogging();

Log.Information("SQL Cmdr starting...");

app.Run();

