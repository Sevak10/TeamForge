using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TeamForge.Web.Controllers;

[AllowAnonymous]
[Route("account")]
public sealed class AuthController : Controller
{
    public const string HardcodedUsername = "sevakn";
    private const string HardcodedPassword = "12345678";

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl = null)
    {
        var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

        if (string.Equals(username?.Trim(), HardcodedUsername, StringComparison.Ordinal) &&
            string.Equals(password, HardcodedPassword, StringComparison.Ordinal))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, HardcodedUsername),
                new(ClaimTypes.NameIdentifier, HardcodedUsername)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            return LocalRedirect(safeReturnUrl);
        }

        TempData["LoginError"] = "Invalid username or password.";
        return RedirectToPage("/Login", new { returnUrl = safeReturnUrl });
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}
