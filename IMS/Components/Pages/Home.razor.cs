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
                    // Redirect admin users immediately
                    NavigationManager.NavigateTo("/dashboard", forceLoad: true);
                }
                // If user is authenticated but not admin, do nothing or redirect somewhere else if needed
                return;
            }

            // Only redirect to login if user is NOT authenticated and is on the home page
            if (NavigationManager.Uri.Equals("https://localhost:7172/"))
            {
                NavigationManager.NavigateTo("/Account/Login", forceLoad: true);
            }
        }
    }
}
