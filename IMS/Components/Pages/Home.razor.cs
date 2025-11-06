using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace IMS.Components.Pages
{
    public partial class Home
    {
        [Inject] public NavigationManager NavigationManager { get; set; }
        [CascadingParameter] private Task<AuthenticationState> AuthenticationStateTask { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                if (user.IsInRole("Admin"))
                {
                    // ✅ redirect admin users immediately
                    NavigationManager.NavigateTo("/dashboard", forceLoad: true);
                    return;
                }
            }
        }
    }
}
