using Serilog;
using SqlCmdr.Abstractions;
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
builder.Services.AddHttpClient("DataApiProxy", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSqlCmdr();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg?v=3"));

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (string.Equals(context.Request.Path, "/favicon.ico", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/favicon.svg?v=3");
        return;
    }

    await next();
});
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (IsSqlCommanderAppPath(context.Request.Path))
    {
        context.Request.Path = "/";
    }

    await next();
});
app.UseRouting();
app.UseAuthorization();
MapDataApiProxyRoutes(app);
app.MapRazorPages();
app.UseSerilogRequestLogging();

Log.Information("SQL Cmdr starting...");

app.Run();

static bool IsSqlCommanderAppPath(PathString path)
{
    return path.Equals("/app", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/app", StringComparison.OrdinalIgnoreCase);
}

static void MapDataApiProxyRoutes(WebApplication app)
{
    var methods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD" };

    app.MapMethods("/data-api", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, string.Empty));

    // Swagger UI uses relative asset URLs such as ./swagger-ui.css. When the page is opened as
    // /data-api/swagger (without a trailing slash), browsers resolve those to /data-api/swagger-ui.css.
    // Keep the advertised URL slash-terminated and also rewrite the common no-slash asset paths.
    app.MapMethods("/data-api/swagger-ui.css", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "swagger/swagger-ui.css"));
    app.MapMethods("/data-api/swagger-ui-bundle.js", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "swagger/swagger-ui-bundle.js"));
    app.MapMethods("/data-api/swagger-ui-standalone-preset.js", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "swagger/swagger-ui-standalone-preset.js"));
    app.MapMethods("/data-api/oauth2-redirect.html", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "swagger/oauth2-redirect.html"));
    app.MapMethods("/data-api/favicon-16x16.png", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "swagger/favicon-16x16.png"));
    app.MapMethods("/data-api/favicon-32x32.png", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "swagger/favicon-32x32.png"));

    // Nitro is hosted by DAB at /graphql, but its generated HTML references assets with relative
    // paths like ./assets/main.js. When SQL Commander exposes Nitro at /data-api/graphql, browsers
    // resolve those assets as /data-api/assets/*. Rewrite those requests back to DAB's /graphql root.
    app.MapMethods("/data-api/assets/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, CombinePath("graphql/assets", path)));
    app.MapMethods("/data-api/manifest.webmanifest", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "graphql/manifest.webmanifest"));
    app.MapMethods("/data-api/favicon.ico", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "graphql/favicon.ico"));
    app.MapMethods("/data-api/favicon.svg", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "graphql/favicon.svg"));
    app.MapMethods("/data-api/apple-touch-icon.png", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, "graphql/apple-touch-icon.png"));

    // Older SQL Commander builds and some user bookmarks may still point at /nitro. DAB 2.0.x
    // serves Nitro from /graphql, so keep /nitro as a compatibility alias instead of returning 400.
    app.MapMethods("/data-api/nitro", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyNitroAsync(context, dataApiBuilderService, httpClientFactory, "graphql"));
    app.MapMethods("/data-api/nitro/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, CombinePath("graphql", path)));

    app.MapMethods("/data-api/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiMaybeNitroAsync(context, dataApiBuilderService, httpClientFactory, path ?? string.Empty));

    // DAB's Swagger UI/OpenAPI document may contain root-relative links such as /swagger or /api.
    // Keep these compatibility routes so the proxied UI works inside Docker without publishing DAB's port.
    app.MapMethods("/swagger/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, CombinePath("swagger", path)));
    app.MapMethods("/api/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, CombinePath("api", path)));
    app.MapMethods("/graphql/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiMaybeNitroAsync(context, dataApiBuilderService, httpClientFactory, CombinePath("graphql", path)));
    app.MapMethods("/nitro", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory)
        => ProxyNitroAsync(context, dataApiBuilderService, httpClientFactory, "graphql"));
    app.MapMethods("/nitro/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, CombinePath("graphql", path)));
    app.MapMethods("/mcp/{**path}", methods, (HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string? path)
        => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, CombinePath("mcp", path)));
}

static Task ProxyNitroAsync(HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string path)
    => ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, path, InjectNitroEndpointBootstrap);

static Task ProxyDataApiMaybeNitroAsync(HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string path)
{
    var normalizedPath = path.Trim('/');
    var isNitroShellPath = string.Equals(normalizedPath, "graphql", StringComparison.OrdinalIgnoreCase);
    return isNitroShellPath
        ? ProxyNitroAsync(context, dataApiBuilderService, httpClientFactory, "graphql")
        : ProxyDataApiAsync(context, dataApiBuilderService, httpClientFactory, path);
}

static async Task ProxyDataApiAsync(HttpContext context, IDataApiBuilderService dataApiBuilderService, IHttpClientFactory httpClientFactory, string path, Func<string, string>? transformHtml = null)
{
    var status = dataApiBuilderService.GetStatus();
    if (!status.Running || string.IsNullOrWhiteSpace(status.BaseUrl))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            errorMessage = "Data API is not running."
        }, context.RequestAborted);
        return;
    }

    var targetUri = BuildDataApiTargetUri(status.BaseUrl, path, context.Request.QueryString);
    using var proxyRequest = CreateProxyRequest(context, targetUri);
    var httpClient = httpClientFactory.CreateClient("DataApiProxy");

    using var proxyResponse = await httpClient.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)proxyResponse.StatusCode;

    foreach (var header in proxyResponse.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var header in proxyResponse.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    context.Response.Headers.Remove("transfer-encoding");
    context.Response.Headers.Remove("connection");
    context.Response.Headers.Remove("keep-alive");
    context.Response.Headers.Remove("proxy-authenticate");
    context.Response.Headers.Remove("proxy-authorization");
    context.Response.Headers.Remove("te");
    context.Response.Headers.Remove("trailer");
    context.Response.Headers.Remove("upgrade");

    if (transformHtml is not null && IsHtmlResponse(proxyResponse))
    {
        context.Response.Headers.Remove("content-length");
        context.Response.Headers.Remove("content-encoding");
        var html = await ReadHtmlResponseAsync(proxyResponse.Content, context.RequestAborted);
        var transformedHtml = transformHtml(html);
        context.Response.ContentLength = System.Text.Encoding.UTF8.GetByteCount(transformedHtml);
        await context.Response.WriteAsync(transformedHtml, context.RequestAborted);
        return;
    }

    await proxyResponse.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
}

static async Task<string> ReadHtmlResponseAsync(HttpContent content, CancellationToken cancellationToken)
{
    var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
    if (bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
    {
        await using var compressedStream = new MemoryStream(bytes);
        await using var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    return System.Text.Encoding.UTF8.GetString(bytes);
}

static bool IsHtmlResponse(HttpResponseMessage response)
{
    var contentType = response.Content.Headers.ContentType?.MediaType;
    return string.Equals(contentType, "text/html", StringComparison.OrdinalIgnoreCase);
}

static string InjectNitroEndpointBootstrap(string html)
{
    const string marker = "sql-commander-nitro-endpoint-bootstrap";
    if (html.Contains(marker, StringComparison.OrdinalIgnoreCase))
    {
        return html;
    }

    const string script = """
    <script id="sql-commander-nitro-endpoint-bootstrap">
    (() => {
        const connectionKind = 'connection-settings';
        const documentKind = 'new-document';
        const markerKey = 'sqlCommanderNitroEndpointConfigured';
        const dbBaseName = 'chillicream-nitro';
        const dbName = self.appNamespace ? `${dbBaseName}-${self.appNamespace}` : dbBaseName;
        const endpoint = new URL(window.location.pathname.replace(/\/$/, ''), window.location.origin).toString();
        const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

        const openExistingDatabase = async () => {
            if (indexedDB.databases) {
                const databases = await indexedDB.databases();
                if (!databases.some(database => database.name === dbName)) {
                    return null;
                }
            }

            return await new Promise(resolve => {
                const request = indexedDB.open(dbName);
                request.onerror = () => resolve(null);
                request.onsuccess = () => resolve(request.result);
            });
        };

        const getAll = (db, storeName) => new Promise(resolve => {
            if (!db.objectStoreNames.contains(storeName)) {
                resolve([]);
                return;
            }

            const request = db.transaction(storeName, 'readonly').objectStore(storeName).getAll();
            request.onerror = () => resolve([]);
            request.onsuccess = () => resolve(request.result || []);
        });

        const put = (db, storeName, value) => new Promise(resolve => {
            const transaction = db.transaction(storeName, 'readwrite');
            transaction.objectStore(storeName).put(value);
            transaction.oncomplete = () => resolve(true);
            transaction.onerror = () => resolve(false);
            transaction.onabort = () => resolve(false);
        });

        const clickBrowseSchema = () => {
            const button = Array.from(document.querySelectorAll('button')).find(element =>
                (element.textContent || '').trim().toLowerCase().includes('browse schema'));
            button?.click();
        };

        const configureEndpoint = async () => {
            if (sessionStorage.getItem(markerKey) === endpoint) {
                return;
            }

            for (let attempt = 0; attempt < 40; attempt += 1) {
                const db = await openExistingDatabase();
                if (db) {
                    try {
                        const attachments = await getAll(db, 'attachment');
                        const items = await getAll(db, 'item');
                        const tabs = await getAll(db, 'tab');
                        const activeTabId = tabs.find(tab => tab?.workspaceId === 'local')?.activeTabId;
                        const document = activeTabId
                            ? items.find(item => item?.id === activeTabId)
                            : items.find(item => item?.kind === documentKind && item?.workspaceId === 'local') ||
                              items.find(item => item?.kind === 'document' && item?.workspaceId === 'local');

                        if (!document) {
                            clickBrowseSchema();
                            await delay(250);
                            continue;
                        }

                        const settings = attachments.find(attachment =>
                            attachment?.kind === connectionKind &&
                            attachment.itemId === document.id);

                        if (settings?.http?.endpoint) {
                            sessionStorage.setItem(markerKey, endpoint);
                            return;
                        }

                        if (settings?.http) {
                            settings.http = {
                                ...settings.http,
                                endpoint,
                                sdlEndpoint: '',
                                includeCookies: false,
                                useGet: false,
                                useTunnel: false,
                                sseSubscriptionEndpoint: '',
                                subscriptionEndpoint: '',
                                subscriptionProtocol: settings.http.subscriptionProtocol || 'auto'
                            };
                            settings.hash = `${Date.now().toString(16)}${Math.random().toString(16).slice(2)}`;

                            if (await put(db, 'attachment', settings)) {
                                sessionStorage.setItem(markerKey, endpoint);
                                window.location.reload();
                                return;
                            }
                        }
                    } finally {
                        db.close();
                    }
                } else {
                    clickBrowseSchema();
                }

                await delay(250);
            }
        };

        window.addEventListener('load', () => setTimeout(() => void configureEndpoint(), 500), { once: true });
    })();
    </script>
    """;

    var bodyIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
    return bodyIndex < 0
        ? html + script
        : html.Insert(bodyIndex, script);
}

static HttpRequestMessage CreateProxyRequest(HttpContext context, Uri targetUri)
{
    var request = context.Request;
    var proxyRequest = new HttpRequestMessage(new HttpMethod(request.Method), targetUri);

    if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsDelete(request.Method) && !HttpMethods.IsTrace(request.Method))
    {
        proxyRequest.Content = new StreamContent(request.Body);
    }

    foreach (var header in request.Headers)
    {
        if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header.Key, "Accept-Encoding", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (!proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
        {
            proxyRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", request.Host.Value);
    proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", request.Scheme);
    proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-PathBase", request.PathBase.Value ?? string.Empty);

    return proxyRequest;
}

static Uri BuildDataApiTargetUri(string baseUrl, string path, QueryString queryString)
{
    var builder = new UriBuilder(baseUrl.TrimEnd('/'))
    {
        Path = CombinePath(new Uri(baseUrl).AbsolutePath.Trim('/'), path),
        Query = queryString.HasValue ? queryString.Value?.TrimStart('?') : null
    };
    return builder.Uri;
}

static string CombinePath(string prefix, string? path)
{
    prefix = prefix.Trim('/');
    path = path?.Trim('/') ?? string.Empty;

    if (string.IsNullOrEmpty(prefix)) return path;
    if (string.IsNullOrEmpty(path)) return prefix;
    return $"{prefix}/{path}";
}

