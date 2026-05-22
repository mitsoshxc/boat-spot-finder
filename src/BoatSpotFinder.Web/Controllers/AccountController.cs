using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Helpers;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Settings;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BoatSpotFinder.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender _emailSender;
    private readonly IInvitationRepository _invitationRepository;
    private readonly IMarinaAdminRepository _marinaAdminRepository;
    private readonly AppSettings _appSettings;
    private readonly IAuditLogger _auditLogger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender,
        IInvitationRepository invitationRepository,
        IMarinaAdminRepository marinaAdminRepository,
        IOptions<AppSettings> appSettings,
        IAuditLogger auditLogger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _invitationRepository = invitationRepository;
        _marinaAdminRepository = marinaAdminRepository;
        _appSettings = appSettings.Value;
        _auditLogger = auditLogger;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is null)
                return View(model);

            _auditLogger.Log(
                userId: user.Id,
                userEmail: user.Email ?? string.Empty,
                action: "Login",
                entityType: "User",
                entityId: user.Id,
                marinaId: null,
                details: null);

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Admin"))
                return Redirect("/admin/dashboard");
            if (roles.Contains("PlaceOwner"))
                return Redirect("/placeowner/marinas");

            return Redirect("/browse");
        }

        if (result.IsNotAllowed)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is not null && !user.IsActive)
            {
                _auditLogger.Log(
                    userId: user.Id,
                    userEmail: user.Email ?? string.Empty,
                    action: "LoginFailed_Deactivated",
                    entityType: "User",
                    entityId: user.Id,
                    marinaId: null,
                    details: null);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated.");
            }
            else
            {
                _auditLogger.Log(
                    userId: user?.Id ?? string.Empty,
                    userEmail: user?.Email ?? model.Email,
                    action: "LoginFailed_EmailUnconfirmed",
                    entityType: "User",
                    entityId: user?.Id ?? string.Empty,
                    marinaId: null,
                    details: null);
                ModelState.AddModelError("unconfirmed_email", "You must confirm your email before logging in.");
            }
            return View(model);
        }

        var failedUser = await _userManager.FindByEmailAsync(model.Email);
        if (failedUser is null)
        {
            _auditLogger.Log(
                userId: string.Empty,
                userEmail: model.Email,
                action: "LoginFailed_UserNotFound",
                entityType: "User",
                entityId: string.Empty,
                marinaId: null,
                details: null);
        }
        else
        {
            _auditLogger.Log(
                userId: failedUser.Id,
                userEmail: failedUser.Email ?? string.Empty,
                action: "LoginFailed_InvalidPassword",
                entityType: "User",
                entityId: failedUser.Id,
                marinaId: null,
                details: null);
        }
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User)!;
        var userEmail = _userManager.GetUserName(User) ?? string.Empty;

        _auditLogger.Log(
            userId: userId,
            userEmail: userEmail,
            action: "Logout",
            entityType: "User",
            entityId: userId,
            marinaId: null,
            details: null);

        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "BoatOwner");

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = $"{_appSettings.BaseUrl}/account/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await _emailSender.SendAsync(
            user.Email!,
            "Confirm your email",
            $"Please confirm your email by clicking <a href='{confirmationLink}'>here</a>.");

        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    public IActionResult RegisterConfirmation() => View();

    [HttpGet]
    [Route("account/confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return View("ConfirmEmail");

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return View("ConfirmEmail");

        TempData["LoginSuccessMessage"] = "Your email has been confirmed. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Route("account/resend-confirmation")]
    public IActionResult ResendConfirmationEmail([FromQuery] string? email)
    {
        var model = new ResendConfirmationViewModel { Email = email ?? string.Empty };
        return View(model);
    }

    [HttpPost]
    [Route("account/resend-confirmation")]
    public async Task<IActionResult> ResendConfirmationEmail(ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = $"{_appSettings.BaseUrl}/account/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

            await _emailSender.SendAsync(
                user.Email!,
                "Confirm your email",
                $"Please confirm your email by clicking <a href='{confirmationLink}'>here</a>.");
        }

        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    [Route("account/invite-register")]
    public async Task<IActionResult> InviteRegister(string token)
    {
        var tokenHash = TokenHasher.Hash(token);
        var invitation = await _invitationRepository.GetByTokenHashAsync(tokenHash);

        if (invitation is null || invitation.IsUsed || invitation.ExpiresAt < DateTimeOffset.UtcNow)
            return NotFound();

        var model = new InviteRegisterViewModel
        {
            Token = token,
            Email = invitation.Email
        };

        return View(model);
    }

    [HttpPost]
    [Route("account/invite-register")]
    public async Task<IActionResult> InviteRegister(InviteRegisterViewModel model)
    {
        var tokenHash = TokenHasher.Hash(model.Token);
        var invitation = await _invitationRepository.GetByTokenHashAsync(tokenHash);

        if (invitation is null || invitation.IsUsed || invitation.ExpiresAt < DateTimeOffset.UtcNow)
            return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "PlaceOwner");

        var marinaAdmin = new MarinaAdmin
        {
            MarinaId = invitation.MarinaId,
            UserId = user.Id,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = invitation.InvitedById
        };

        await _marinaAdminRepository.AddAsync(marinaAdmin);
        await _invitationRepository.MarkUsedAsync(invitation);

        TempData["LoginSuccessMessage"] = "Your account has been created. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user is not null && await _userManager.IsEmailConfirmedAsync(user))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = $"{_appSettings.BaseUrl}/account/reset-password?email={Uri.EscapeDataString(model.Email)}&token={Uri.EscapeDataString(token)}";
            await _emailSender.SendAsync(model.Email, "Reset your password", $"Reset your password by clicking <a href='{resetLink}'>here</a>.");
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    [Route("account/reset-password")]
    public IActionResult ResetPassword([FromQuery] string email, [FromQuery] string token)
    {
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [Route("account/reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
            return RedirectToAction(nameof(ResetPasswordConfirmation));

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
            return RedirectToAction(nameof(ResetPasswordConfirmation));

        foreach (var e in result.Errors)
            ModelState.AddModelError(string.Empty, e.Description);

        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    [HttpGet]
    [Route("account/access-denied")]
    public IActionResult AccessDenied() => View();
}
