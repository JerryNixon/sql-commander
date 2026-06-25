using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SqlCmdr.Abstractions;
using SqlCmdr.Models;
using SqlCmdr.Services;
using SqlCmdr.Web.Pages;

namespace SqlCmdr.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "SettingsIsolation")]
public class SettingsIsolationTests
{
    [Fact]
    public async Task SavingSettingsForOneBrowser_DoesNotChangeSettingsReturnedToBrowserWithoutCookie()
    {
        var settingsService = new SettingsService(new ConfigurationBuilder().Build(), NullLogger<SettingsService>.Instance);
        var dataProtectionProvider = DataProtectionProvider.Create("SqlCmdr.Tests.SettingsIsolation");
        var userSettings = new AppSettings
        {
            Server = "prod.database.windows.net",
            Database = "ProdDb",
            UserId = "prod-admin",
            Password = "SuperSecret!",
            AuthenticationType = AuthenticationType.SqlAuthentication
        };

        var firstBrowser = CreatePageModel(settingsService, dataProtectionProvider);
        await firstBrowser.OnPostSettingsAsync(userSettings);

        var secondBrowser = CreatePageModel(settingsService, dataProtectionProvider);
        var result = await secondBrowser.OnGetSettingsAsync();

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var settings = json.Value.Should().BeOfType<AppSettings>().Subject;
        settings.Server.Should().BeEmpty();
        settings.UserId.Should().BeEmpty();
        settings.Password.Should().BeEmpty();
    }

    static IndexModel CreatePageModel(ISettingsService settingsService, IDataProtectionProvider dataProtectionProvider)
    {
        var model = new IndexModel(
            NullLogger<IndexModel>.Instance,
            settingsService,
            Mock.Of<IMetadataService>(),
            Mock.Of<IQueryExecutionService>(),
            Mock.Of<IDataApiBuilderService>(),
            dataProtectionProvider);

        model.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return model;
    }
}