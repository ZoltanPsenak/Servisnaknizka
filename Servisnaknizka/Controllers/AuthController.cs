using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Servisnaknizka.Models;
using Servisnaknizka.Services;
using System.Security.Claims;

namespace Servisnaknizka.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;

    public AuthController(SignInManager<User> signInManager, UserManager<User> userManager, IEmailService emailService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _emailService = emailService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Redirect("/login?error=invalid");
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || string.IsNullOrEmpty(user.UserName))
        {
            return Redirect("/login?error=invalid");
        }

        if (!user.IsActive)
        {
            return Redirect("/login?error=invalid");
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true
        );

        if (result.Succeeded)
        {
            var returnUrl = "/dashboard";
            if (!string.IsNullOrEmpty(request.ReturnUrl) && Url.IsLocalUrl(request.ReturnUrl))
            {
                returnUrl = request.ReturnUrl;
            }
            return Redirect(returnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            var returnUrl = "/dashboard";
            if (!string.IsNullOrEmpty(request.ReturnUrl) && Url.IsLocalUrl(request.ReturnUrl))
            {
                returnUrl = request.ReturnUrl;
            }
            return Redirect($"/login-2fa?returnUrl={Uri.EscapeDataString(returnUrl)}&rememberMe={request.RememberMe}");
        }
        
        if (result.IsLockedOut)
        {
            return Redirect("/login?error=locked");
        }

        return Redirect("/login?error=invalid");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) ||
            string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName))
        {
            return Redirect("/register?error=invalid");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return Redirect("/register?error=passwords");
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Redirect("/register?error=exists");
        }

        // Určenie roly - admin sa nedá registrovať cez formulár
        var role = request.Role switch
        {
            "Service" => UserRole.Service,
            _ => UserRole.Owner
        };

        var roleName = role switch
        {
            UserRole.Service => "Service",
            _ => "Owner"
        };

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserName = request.Email,
            Email = request.Email,
            Role = role,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, roleName);
            await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Role, roleName));
            return Redirect("/login?success=registered");
        }

        // Ak heslo nespĺňa požiadavky
        return Redirect("/register?error=password_weak");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    [HttpPost("login-2fa")]
    public async Task<IActionResult> LoginWith2fa([FromForm] TwoFactorRequest request)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return Redirect("/login?error=invalid");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Redirect("/login-2fa?error=invalid_code");
        }

        var authenticatorCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
            authenticatorCode,
            request.RememberMe,
            rememberClient: false
        );

        if (result.Succeeded)
        {
            var returnUrl = "/dashboard";
            if (!string.IsNullOrEmpty(request.ReturnUrl) && Url.IsLocalUrl(request.ReturnUrl))
            {
                returnUrl = request.ReturnUrl;
            }
            return Redirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            return Redirect("/login?error=locked");
        }

        return Redirect("/login-2fa?error=invalid_code");
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Redirect("/forgot-password?error=invalid");
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user != null && user.IsActive)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Uri.EscapeDataString(token);
            var encodedEmail = Uri.EscapeDataString(request.Email);

            var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?email={encodedEmail}&token={encodedToken}";

            var htmlBody = $@"
                <div style='font-family:Arial,sans-serif;max-width:500px;margin:0 auto;padding:20px'>
                    <h2 style='color:#2563eb'>Servisná Knižka</h2>
                    <p>Dobrý deň,</p>
                    <p>Prijali sme žiadosť o obnovenie hesla pre váš účet.</p>
                    <p style='margin:24px 0'>
                        <a href='{resetUrl}' style='background:#2563eb;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold'>
                            Obnoviť heslo
                        </a>
                    </p>
                    <p style='font-size:13px;color:#64748b'>Ak ste o obnovenie hesla nežiadali, tento e-mail ignorujte. Odkaz je platný 24 hodín.</p>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(request.Email, "Obnovenie hesla – Servisná Knižka", htmlBody);
            }
            catch
            {
                // Log error but don't reveal whether user exists
            }
        }

        // Always show success to prevent email enumeration
        return Redirect("/forgot-password?success=sent");
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromForm] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return Redirect("/reset-password?error=invalid_token");
        }

        if (request.Password != request.ConfirmPassword)
        {
            var encodedToken = Uri.EscapeDataString(request.Token);
            var encodedEmail = Uri.EscapeDataString(request.Email);
            return Redirect($"/reset-password?email={encodedEmail}&token={encodedToken}&error=passwords");
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Redirect("/reset-password?error=invalid_token");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (result.Succeeded)
        {
            return Redirect("/reset-password?success=reset");
        }

        var encodedToken2 = Uri.EscapeDataString(request.Token);
        var encodedEmail2 = Uri.EscapeDataString(request.Email);

        if (result.Errors.Any(e => e.Code == "InvalidToken"))
        {
            return Redirect($"/reset-password?email={encodedEmail2}&token={encodedToken2}&error=invalid_token");
        }

        return Redirect($"/reset-password?email={encodedEmail2}&token={encodedToken2}&error=password_weak");
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = false;
    public string? ReturnUrl { get; set; }
}

public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Role { get; set; } = "Owner";
}

public class TwoFactorRequest
{
    public string Code { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = false;
    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}