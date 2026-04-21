using Leaf.Web.Features.Auth;
using Leaf.Web.Features.Projects;
using Leaf.Web.Features.Shared;
using Leaf.Web.UI.Infrastructure;
using Leaf.Web.UI.Models.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.UI.Controllers;

[Route("auth")]
public sealed class AuthController : LeafControllerBase
{
    private readonly IAuthService _authService;
    private const string RememberCookie = "leaf_remember_user";

    public AuthController(
        IAuthService authService,
        ILeafRepository repository,
        IProjectService projectService)
        : base(repository, projectService)
    {
        _authService = authService;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (CurrentUserId.HasValue)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel());
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(model.Email, model.Password, ct);
        if (result.IsFailure)
        {
            model.Error = result.Error.Message;
            return View(model);
        }

        HttpContext.Session.SetInt32(SessionKeys.UserId, result.Value.Id);

        if (model.Remember)
        {
            Response.Cookies.Append(RememberCookie, result.Value.Id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }
        else
        {
            Response.Cookies.Delete(RememberCookie);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete(RememberCookie);
        return RedirectToAction("Login");
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login");
        }

        var user = await Repository.GetUserByIdAsync(CurrentUserId.Value, ct);
        if (user is null)
        {
            return RedirectToAction("Login");
        }

        var vm = new ProfileViewModel
        {
            Shell = await BuildShellAsync("Profile", ct: ct),
            User = user
        };

        return View(vm);
    }
}
