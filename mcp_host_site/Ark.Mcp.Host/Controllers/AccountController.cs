using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using McpServiceHub.Data;
using McpServiceHub.Models.Entities;
using McpServiceHub.Models.ViewModels;
using McpServiceHub.Services.Interfaces;

namespace McpServiceHub.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AppDbContext context,
        IEmailSender emailSender,
        IOtpService otpService,
        IConfiguration configuration,
        ILogger<AccountController> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _otpService = otpService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendOtp(SendOtpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Login", model);
        }

        var email = model.Email.Trim();

        // Find or create user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            user = new User { Email = email };
            _context.Users.Add(user);
        }

        // Generate OTP
        var otp = _otpService.GenerateOtp();
        var expiryMinutes = _configuration.GetValue<int>("OtpSettings:ExpiryMinutes", 10);
        
        user.OtpCode = otp;
        user.OtpExpiration = DateTime.UtcNow.AddMinutes(expiryMinutes);
        
        await _context.SaveChangesAsync();

        // Send email
        try
        {
            var emailBody = $@"
                <h2>MCP Service Hub - Login OTP</h2>
                <p>Your one-time password is:</p>
                <h1 style='font-size: 32px; letter-spacing: 2px;'>{otp}</h1>
                <p>This code will expire in {expiryMinutes} minutes.</p>
                <p>If you didn't request this, please ignore this email.</p>
                <hr/>
                <small>MCP Service Hub - Secure Authentication</small>";

            await _emailSender.SendEmailAsync(email, "Your ARK-MCP Hub OTP Code", emailBody);
            _logger.LogInformation($"OTP sent to {email}");
            
            TempData["SuccessMessage"] = "OTP sent successfully! Please check your email.";
            return RedirectToAction("VerifyOtp", new { email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email");
            return RedirectToAction("VerifyOtp", new { email });
            ModelState.AddModelError("", "Failed to send OTP email. Please try again.");
            return View("Login", model);
        }
    }

    [HttpGet]
    public IActionResult VerifyOtp(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login");
        }

        var model = new VerifyOtpViewModel { Email = email };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            ModelState.AddModelError("", "Invalid email or OTP");
            return View(model);
        }

        if (!_otpService.IsOtpValid(model.OtpCode, user.OtpCode, user.OtpExpiration))
        {
            ModelState.AddModelError("", "Invalid or expired OTP. Please request a new one.");
            return View(model);
        }

        // Clear OTP after successful verification
        user.OtpCode = null;
        user.OtpExpiration = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Create authentication claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(model.RememberMe ? 30 : 1)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        TempData["SuccessMessage"] = $"Welcome back, {user.Email}!";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "You have been logged out.";
        return RedirectToAction("Login");
    }

    [HttpPost]
    public async Task<IActionResult> ResendOtp(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("Email is required");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return NotFound("User not found");
        }

        var otp = _otpService.GenerateOtp();
        var expiryMinutes = _configuration.GetValue<int>("OtpSettings:ExpiryMinutes", 10);
        
        user.OtpCode = otp;
        user.OtpExpiration = DateTime.UtcNow.AddMinutes(expiryMinutes);
        await _context.SaveChangesAsync();

        var emailBody = $@"
            <h2>MCP Service Hub - New OTP</h2>
            <p>Your new one-time password is:</p>
            <h1 style='font-size: 32px; letter-spacing: 2px;'>{otp}</h1>
            <p>This code will expire in {expiryMinutes} minutes.</p>";

        await _emailSender.SendEmailAsync(email, "Your New MCP Hub OTP Code", emailBody);
        
        return Ok(new { message = "OTP resent successfully" });
    }
}