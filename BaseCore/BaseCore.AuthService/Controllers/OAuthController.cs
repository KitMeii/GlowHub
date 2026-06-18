using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using BaseCore.Common;
using BaseCore.Services.Authen;
using System.Security.Claims;

namespace BaseCore.AuthService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OAuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _config;
        private const string SecretKey = "YourSecretKeyForAuthenticationShouldBeLongEnough";
        private const int TokenExpirationMinutes = 480;

        public OAuthController(IUserService userService, IConfiguration config)
        {
            _userService = userService;
            _config = config;
        }

        private string FrontendUrl =>
            _config["Frontend:BaseUrl"] ?? "http://127.0.0.1:5500/WebClient/WebClient/public/kaira-1.0.0";

        // GET /api/oauth/google — bắt đầu luồng Google OAuth
        [HttpGet("google")]
        public IActionResult LoginWithGoogle()
        {
            var redirectUri = Url.Action(nameof(GoogleCallback), "OAuth", null, Request.Scheme);
            var properties  = new AuthenticationProperties { RedirectUri = redirectUri };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // GET /api/oauth/google/callback — Google redirect về đây sau khi user đồng ý
        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            try
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                if (!result.Succeeded)
                    return Redirect($"{FrontendUrl}/login.html?error=google_auth_failed");

                var oauthId = result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier);
                var name    = result.Principal!.FindFirstValue(ClaimTypes.Name);
                var email   = result.Principal!.FindFirstValue(ClaimTypes.Email);

                if (string.IsNullOrEmpty(oauthId))
                    return Redirect($"{FrontendUrl}/login.html?error=missing_google_id");

                var user = await _userService.FindOrCreateOAuthUserAsync("GOOGLE", oauthId, name, email);
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return BuildTokenRedirect(user);
            }
            catch (Exception ex)
            {
                return Redirect($"{FrontendUrl}/login.html?error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        // GET /api/oauth/facebook — bắt đầu luồng Facebook OAuth
        [HttpGet("facebook")]
        public IActionResult LoginWithFacebook()
        {
            var redirectUri = Url.Action(nameof(FacebookCallback), "OAuth", null, Request.Scheme);
            var properties  = new AuthenticationProperties { RedirectUri = redirectUri };
            return Challenge(properties, FacebookDefaults.AuthenticationScheme);
        }

        // GET /api/oauth/facebook/callback — Facebook redirect về đây
        [HttpGet("facebook/callback")]
        public async Task<IActionResult> FacebookCallback()
        {
            try
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                if (!result.Succeeded)
                    return Redirect($"{FrontendUrl}/login.html?error=facebook_auth_failed");

                var oauthId = result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier);
                var name    = result.Principal!.FindFirstValue(ClaimTypes.Name);
                var email   = result.Principal!.FindFirstValue(ClaimTypes.Email);

                if (string.IsNullOrEmpty(oauthId))
                    return Redirect($"{FrontendUrl}/login.html?error=missing_facebook_id");

                var user = await _userService.FindOrCreateOAuthUserAsync("FACEBOOK", oauthId, name, email);
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return BuildTokenRedirect(user);
            }
            catch (Exception ex)
            {
                return Redirect($"{FrontendUrl}/login.html?error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private IActionResult BuildTokenRedirect(BaseCore.Entities.User user)
        {
            if (!user.IsActive)
                return Redirect($"{FrontendUrl}/login.html?error=account_banned");

            var role  = user.UserType switch { 1 => RoleConstant.Admin, 2 => RoleConstant.Seller, _ => RoleConstant.User };
            var token = TokenHelper.GenerateToken(SecretKey, TokenExpirationMinutes, user.Id, user.UserName, role, user.TokenVersion);

            var url = $"{FrontendUrl}/login.html" +
                      $"?token={Uri.EscapeDataString(token)}" +
                      $"&userId={Uri.EscapeDataString(user.Id)}" +
                      $"&role={Uri.EscapeDataString(role)}" +
                      $"&name={Uri.EscapeDataString(user.Name ?? "")}" +
                      $"&email={Uri.EscapeDataString(user.Email ?? "")}";

            return Redirect(url);
        }
    }
}
