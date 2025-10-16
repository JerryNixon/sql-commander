using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SqlCmdr.Library.Models;
using SqlCmdr.Library.Abstractions;
using System.Text.Json;

namespace SqlCmdr.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IMetadataService _metadataService;
    private readonly IQueryExecutionService _queryExecutionService;

    public IndexModel(
        ILogger<IndexModel> logger,
        ISettingsService settingsService,
        IMetadataService metadataService,
        IQueryExecutionService queryExecutionService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _metadataService = metadataService;
        _queryExecutionService = queryExecutionService;
    }

    public string ServerName { get; set; } = "Not Connected";
    public string DatabaseName { get; set; } = "N/A";

    public async Task OnGetAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (!string.IsNullOrWhiteSpace(settings.Server))
        {
            ServerName = settings.Server;
            DatabaseName = settings.Database;
        }
    }

    public async Task<IActionResult> OnGetSettingsAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        return new JsonResult(settings);
    }

    public async Task<IActionResult> OnPostSettingsAsync([FromBody] AppSettings settings)
    {
        await _settingsService.SaveSettingsAsync(settings);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnDeleteSettingsAsync()
    {
        await _settingsService.DeleteSettingsAsync();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostTestConnectionAsync([FromBody] AppSettings settings)
    {
        try
        {
            _logger.LogInformation("Test connection request received");
            if (settings == null) return BadRequest(new { success = false, errorMessage = "Invalid settings data" });
            if (string.IsNullOrWhiteSpace(settings.Server)) return BadRequest(new { success = false, errorMessage = "Server is required" });
            var connectionString = settings.ToConnectionString();
            _logger.LogInformation("Testing connection to {Server}/{Database}", settings.Server, settings.Database);
            var result = await _metadataService.TestConnectionAsync(connectionString);
            _logger.LogInformation("Test connection result: {Success}", result.Success);
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test connection handler failed");
            return new JsonResult(new ConnectionTestResult { Success = false, ErrorMessage = $"Handler exception: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnGetMetadataAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var connectionString = settings.ToConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(settings.Server)) return new JsonResult(new { success = false, error = "No connection configured" });
            var metadata = await _metadataService.GetMetadataAsync(connectionString);
            return new JsonResult(new { success = true, data = metadata });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load metadata");
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostExecuteQueryAsync([FromBody] QueryRequest request)
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var connectionString = settings.ToConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(settings.Server)) return new JsonResult(new QueryResponse { Success = false, ErrorMessage = "No connection configured" });
            var requestWithLimit = request with { ResultLimit = request.ResultLimit ?? settings.DefaultResultLimit };
            var result = await _queryExecutionService.ExecuteQueryAsync(connectionString, requestWithLimit, HttpContext.RequestAborted);
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query");
            return new JsonResult(new QueryResponse { Success = false, ErrorMessage = ex.Message });
        }
    }

    public IActionResult OnPostCancelQueryAsync()
    {
        _queryExecutionService.CancelCurrentQuery();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnGetDownloadSchemaAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var connectionString = settings.ToConnectionString();
            var metadata = await _metadataService.GetMetadataAsync(connectionString);
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            var fileName = $"{settings.Server}_{settings.Database}_schema_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download schema");
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }
}

