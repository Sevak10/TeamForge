using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TeamForge.Web.Pages;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var target = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
            return LocalRedirect(target);
        }

        if (TempData.TryGetValue("LoginError", out var error) && error is string errorText)
        {
            ErrorMessage = errorText;
        }

        return Page();
    }
}
