using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SqlCmdr.Helpers;
using SqlCmdr.Abstractions;
using SqlCmdr.Models;
using System.Text.Json;

namespace SqlCmdr.Web.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel : PageModel
{
    private const string SettingsCookieName = "SqlCmdr.ConnectionSettings.v1";
    private const string LegacySettingsCookieName = "SqlCmdr.Settings";

    private readonly ILogger<IndexModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IMetadataService _metadataService;
    private readonly IQueryExecutionService _queryExecutionService;
    private readonly IDataApiBuilderService _dataApiBuilderService;
    private readonly IDataProtector _settingsProtector;

    public IndexModel(
        ILogger<IndexModel> logger,
        ISettingsService settingsService,
        IMetadataService metadataService,
        IQueryExecutionService queryExecutionService,
        IDataApiBuilderService dataApiBuilderService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _logger = logger;
        _settingsService = settingsService;
        _metadataService = metadataService;
        _queryExecutionService = queryExecutionService;
        _dataApiBuilderService = dataApiBuilderService;
        _settingsProtector = (dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider)))
            .CreateProtector("SqlCmdr.Web.ConnectionSettingsCookie.v1");
    }

    public string ServerName { get; set; } = "Not Connected";
    public string DatabaseName { get; set; } = "N/A";

    public async Task OnGetAsync()
    {
        var settings = await GetEffectiveSettingsAsync();
        if (!string.IsNullOrWhiteSpace(settings.Server))
        {
            ServerName = settings.Server;
            DatabaseName = settings.Database;
        }
    }

    public async Task<IActionResult> OnGetSettingsAsync()
    {
        var settings = await GetEffectiveSettingsAsync();
        return new JsonResult(settings);
    }

    public async Task<IActionResult> OnPostSettingsAsync([FromBody] AppSettings settings)
    {
        if (settings == null)
        {
            return BadRequest(new { success = false, errorMessage = "Invalid settings data" });
        }

        var saved = settings.Normalize();
        WriteSettingsCookie(saved);
        return await Task.FromResult(new JsonResult(new { success = true, settings = saved }));
    }

    public IActionResult OnPostParseConnectionString([FromBody] JsonElement request)
    {
        try
        {
            var connectionString = request.TryGetProperty("connectionString", out var value) ? value.GetString() : string.Empty;
            var parsed = AppSettings.FromConnectionString(connectionString ?? string.Empty);
            return new JsonResult(new { success = true, settings = parsed });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse connection string");
            return new JsonResult(new { success = false, errorMessage = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostTestConnectionAsync([FromBody] AppSettings settings)
    {
        try
        {
            _logger.LogInformation("Test connection request received");
            if (settings == null) return BadRequest(new { success = false, errorMessage = "Invalid settings data" });
            if (string.IsNullOrWhiteSpace(settings.Server)) return BadRequest(new { success = false, errorMessage = "Server is required" });

            _logger.LogInformation("Testing connection to {Server}/{Database} using {AuthType}",
                settings.Server, settings.Database, settings.AuthenticationType);

            var result = await _metadataService.TestConnectionAsync(settings);
            _logger.LogInformation("Test connection result: {Success}", result.Success);
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test connection handler failed");
            return new JsonResult(new ConnectionTestResult { Success = false, ErrorMessage = $"Handler exception: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostDatabasesAsync([FromBody] AppSettings settings)
    {
        try
        {
            if (settings == null) return BadRequest(new DatabaseListResult { Success = false, ErrorMessage = "Invalid settings data" });
            if (string.IsNullOrWhiteSpace(settings.Server)) return BadRequest(new DatabaseListResult { Success = false, ErrorMessage = "Server is required" });

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));

            var result = await _metadataService.ListDatabasesAsync(settings, timeout.Token);
            return new JsonResult(result);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return new JsonResult(new DatabaseListResult
            {
                Success = false,
                ErrorMessage = "Timed out while loading databases. Check the server name, credentials, and network connectivity."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List databases handler failed");
            return new JsonResult(new DatabaseListResult { Success = false, ErrorMessage = $"Handler exception: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnGetMetadataAsync()
    {
        try
        {
            var settings = await GetEffectiveSettingsAsync();
            if (string.IsNullOrWhiteSpace(settings.Server))
            {
                return new JsonResult(new { success = false, error = "No connection configured" });
            }

            var metadata = await _metadataService.GetMetadataAsync(settings);
            return new JsonResult(new { success = true, data = metadata });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load metadata");
            return new JsonResult(new { success = false, message = ex.Message, details = ex.ToString() });
        }
    }

    public async Task<IActionResult> OnPostExecuteQueryAsync([FromBody] QueryRequest request)
    {
        try
        {
            if (request == null)
            {
                var invalidRequest = new QueryErrorDiagnostic(
                    "invalid-request",
                    "The query request was invalid.",
                    "No query request body was received.",
                    "Refresh the page and try again.");
                return new JsonResult(invalidRequest.ToFailedResponse());
            }

            var settings = await GetEffectiveSettingsAsync();
            if (string.IsNullOrWhiteSpace(settings.Server))
            {
                var noConnection = new QueryErrorDiagnostic(
                    "no-connection",
                    "No connection configured.",
                    "SQL Commander does not have a server configured for this browser session.",
                    "Open Settings, enter connection details, save them, and try the query again.",
                    TroubleshootingSteps:
                    [
                        "Open Settings from the status bar or gear button.",
                        "Use Test Connection before running the query again."
                    ]);
                return new JsonResult(noConnection.ToFailedResponse());
            }

            var requestWithLimit = request with { ResultLimit = request.ResultLimit ?? settings.DefaultResultLimit };
            var result = await _queryExecutionService.ExecuteQueryAsync(settings, requestWithLimit, HttpContext.RequestAborted);
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query");
            return new JsonResult(QueryErrorDiagnostics.FromException(ex).ToFailedResponse());
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
            var settings = await GetEffectiveSettingsAsync();
            var metadata = await _metadataService.GetMetadataAsync(settings);
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

    public async Task<IActionResult> OnPostGenerateDabConfigAsync([FromBody] DataApiBuilderGenerateRequest request)
    {
        try
        {
            var settings = await GetEffectiveSettingsAsync();
            if (string.IsNullOrWhiteSpace(settings.Server))
            {
                return new JsonResult(new DataApiBuilderGenerateResponse
                {
                    Success = false,
                    ErrorMessage = "No connection configured"
                });
            }

            var metadata = await _metadataService.GetMetadataAsync(settings);
            var result = await _dataApiBuilderService.GenerateConfigAsync(settings, metadata, request ?? new DataApiBuilderGenerateRequest(), HttpContext.RequestAborted);
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Data API config");
            return new JsonResult(new DataApiBuilderGenerateResponse
            {
                Success = false,
                ErrorMessage = "Failed to generate the Data API config. Check the SQL Commander logs for details."
            });
        }
    }

    public IActionResult OnGetDataApiStatus()
    {
        return new JsonResult(WithBrowserDataApiUrls(_dataApiBuilderService.GetStatus()));
    }

    public async Task<IActionResult> OnPostStartDataApiAsync([FromBody] DataApiBuilderStartRequest request)
    {
        try
        {
            var settings = await GetEffectiveSettingsAsync();
            if (string.IsNullOrWhiteSpace(settings.Server))
            {
                return new JsonResult(new DataApiRuntimeResponse
                {
                    Success = false,
                    State = "failed",
                    ErrorMessage = "No connection configured"
                });
            }

            var metadata = await _metadataService.GetMetadataAsync(settings);
            var result = await _dataApiBuilderService.StartAsync(settings, metadata, request ?? new DataApiBuilderStartRequest(), HttpContext.RequestAborted);
            return new JsonResult(WithBrowserDataApiUrls(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Data API");
            return new JsonResult(new DataApiRuntimeResponse
            {
                Success = false,
                State = "failed",
                ErrorMessage = "Failed to start Data API. Check the SQL Commander logs for details."
            });
        }
    }

    public async Task<IActionResult> OnPostStopDataApiAsync()
    {
        try
        {
            var result = await _dataApiBuilderService.StopAsync(HttpContext.RequestAborted);
            return new JsonResult(WithBrowserDataApiUrls(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Data API");
            return new JsonResult(new DataApiRuntimeResponse
            {
                Success = false,
                State = "failed",
                ErrorMessage = "Failed to stop Data API. Check the SQL Commander logs for details."
            });
        }
    }

    private DataApiRuntimeResponse WithBrowserDataApiUrls(DataApiRuntimeResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.BaseUrl))
        {
            return response;
        }

        var proxyBaseUrl = BuildDataApiProxyBaseUrl();
        return response with
        {
            BaseUrl = proxyBaseUrl,
            HealthUrl = CombineUrl(proxyBaseUrl, "health"),
            SwaggerUrl = CombineUrl(proxyBaseUrl, "swagger/"),
            NitroUrl = CombineUrl(proxyBaseUrl, "graphql")
        };
    }

    private string BuildDataApiProxyBaseUrl()
    {
        var pathBase = HttpContext.Request.PathBase.Value?.TrimEnd('/') ?? string.Empty;
        return string.IsNullOrEmpty(pathBase) ? "/data-api" : $"{pathBase}/data-api";
    }

    private async Task<AppSettings> GetEffectiveSettingsAsync()
    {
        var cookieSettings = TryReadSettingsCookie();
        if (cookieSettings is not null)
        {
            return cookieSettings;
        }

        return await _settingsService.GetSettingsAsync().ConfigureAwait(false);
    }

    private AppSettings? TryReadSettingsCookie()
    {
        if (!Request.Cookies.TryGetValue(SettingsCookieName, out var protectedValue) || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            var json = _settingsProtector.Unprotect(protectedValue);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return settings?.Normalize();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discarding unreadable SQL Commander settings cookie.");
            Response.Cookies.Delete(SettingsCookieName, new CookieOptions { Path = "/" });
            return null;
        }
    }

    private void WriteSettingsCookie(AppSettings settings)
    {
        var normalized = settings.Normalize();
        var json = JsonSerializer.Serialize(normalized);
        var protectedValue = _settingsProtector.Protect(json);

        Response.Cookies.Append(SettingsCookieName, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(30),
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        });

        Response.Cookies.Delete(LegacySettingsCookieName, new CookieOptions { Path = "/" });
    }

    private static string CombineUrl(string baseUrl, string path)
        => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
