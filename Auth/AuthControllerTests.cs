using FluentAssertions;
using GLMS_CORE_APP.Controllers;
using GLMS_CORE_APP.Services;
using GLMS_CORE_APP.Tests.Helpers;
using GLMS_CORE_APP.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GLMS_CORE_APP.Tests.Auth;

public class AuthControllerTests : IDisposable
{
    private readonly GLMS_CORE_APP.Shared.Data.GlmsDbContext _db;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _db = DbContextFactory.Create();

        var httpClient = new HttpClient(new FakeHttpHandler());
        httpClient.BaseAddress = new Uri("http://localhost");
        var apiClient = new GlmsApiClient(httpClient);

        _controller = new AuthController(apiClient, NullLogger<AuthController>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private void SetSession(string? userId = null, string? userRole = null)
    {
        var session = SessionFactory.Create(userId, userRole: userRole);
        var ctx = HttpContextFactory.Create(session);
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = ctx
        };
    }

    // ── Login GET ──────────────────────────────────────────────────────────

    [Fact]
    public void Login_Get_ReturnsView_WhenNotAuthenticated()
    {
        SetSession();
        _controller.Login().Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Login_Get_RedirectsToHome_WhenAlreadyLoggedIn()
    {
        SetSession(userId: Guid.NewGuid().ToString());
        _controller.Login().Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Index");
    }

    // ── Register GET ───────────────────────────────────────────────────────

    [Fact]
    public void Register_Get_ReturnsView_WhenNotAuthenticated()
    {
        SetSession();
        _controller.Register().Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Register_Get_RedirectsToHome_WhenAlreadyLoggedIn()
    {
        SetSession(userId: Guid.NewGuid().ToString());
        _controller.Register().Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Index");
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [Fact]
    public void Logout_Post_RedirectsToLogin()
    {
        SetSession(userId: Guid.NewGuid().ToString(), userRole: "Admin");
        _controller.Logout().Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Login");
    }
}

// ── Fake HTTP handler for GlmsApiClient in tests ──────────────────────────
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
}