using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Meth.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel(IWebHostEnvironment environment) : PageModel
    {
        public string? EmailConfirmationUrl { get; private set; }

        public void OnGet()
        {
            if (environment.IsDevelopment())
            {
                EmailConfirmationUrl = TempData[nameof(EmailConfirmationUrl)] as string;
            }
        }
    }
}
