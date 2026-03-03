using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Servisnaknizka.Models;
using System.Security.Claims;

namespace Servisnaknizka.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;

    public AuthController(SignInManager<User> signInManager, UserManager<User> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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
            var returnUrl = string.IsNullOrEmpty(request.ReturnUrl) ? "/dashboard" : request.ReturnUrl;
            return Redirect(returnUrl);
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
    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
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