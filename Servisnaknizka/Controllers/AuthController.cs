using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Servisnaknizka.Models;

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

    [HttpPost("logout")]
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