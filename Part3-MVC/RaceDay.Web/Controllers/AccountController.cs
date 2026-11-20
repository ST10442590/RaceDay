using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

// Login, registration, logout and the user's own profile.
public class AccountController : RaceDayControllerBase
{
    public AccountController(ApiClient api) : base(api)
    {
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.IsLoggedIn())
        {
            return RedirectToHome();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Start from a clean session so no previous user's API cookie is sent.
        HttpContext.Session.SignOut();

        var result = await Api.PostAsync<UserProfile>("api/auth/login",
            new LoginRequest { Email = model.Email, Password = model.Password });

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Login failed.");
            return View(model);
        }

        HttpContext.Session.SignIn(result.Data);
        Flash($"Welcome back, {result.Data.FirstName}!");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToHome();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.IsLoggedIn())
        {
            return RedirectToHome();
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role == Roles.Organiser && string.IsNullOrWhiteSpace(model.OrganiserInviteCode))
        {
            ModelState.AddModelError(nameof(model.OrganiserInviteCode),
                "Organisers need the invite code supplied by RaceDay.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new RegisterRequest
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            Password = model.Password,
            Role = model.Role,
            OrganiserInviteCode = model.Role == Roles.Organiser ? model.OrganiserInviteCode : null,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender,
            City = model.City
        };

        var registered = await Api.PostAsync<UserProfile>("api/auth/register", request);
        if (!registered.Success)
        {
            ModelState.AddModelError(string.Empty, registered.Error ?? "Registration failed.");
            return View(model);
        }

        // Log the new user straight in.
        HttpContext.Session.SignOut();
        var login = await Api.PostAsync<UserProfile>("api/auth/login",
            new LoginRequest { Email = model.Email, Password = model.Password });

        if (!login.Success || login.Data is null)
        {
            Flash("Your account was created. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        HttpContext.Session.SignIn(login.Data);
        Flash($"Welcome to RaceDay, {login.Data.FirstName}!");
        return RedirectToHome();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // End the API session first (best effort), then the MVC session.
        await Api.SendWithoutResultAsync(HttpMethod.Post, "api/auth/logout");
        HttpContext.Session.SignOut();

        Flash("You have been logged out.", "info");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [RoleRequired]
    public async Task<IActionResult> Profile()
    {
        var result = await Api.GetAsync<UserProfile>("api/users/me");
        if (!result.Success || result.Data is null)
        {
            return ApiError(result);
        }

        var p = result.Data;
        return View(new ProfileViewModel
        {
            Email = p.Email,
            Role = p.Role,
            MemberSince = p.CreatedAt,
            FirstName = p.FirstName,
            LastName = p.LastName,
            PhoneNumber = p.PhoneNumber,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            City = p.City
        });
    }

    [HttpPost]
    [RoleRequired]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await Api.PutAsync<UserProfile>("api/users/me", new UpdateProfileRequest
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender,
            City = model.City
        });

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Your profile could not be saved.");
            return View(model);
        }

        // Keep the name in the navigation bar up to date.
        HttpContext.Session.SignIn(result.Data);
        Flash("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    [RoleRequired]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [RoleRequired]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await Api.SendWithoutResultAsync(HttpMethod.Put, "api/users/me/password",
            new ChangePasswordRequest
            {
                CurrentPassword = model.CurrentPassword,
                NewPassword = model.NewPassword
            });

        var authFailure = HandleAuthFailure(result);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Your password could not be changed.");
            return View(model);
        }

        Flash("Your password has been changed.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // Organisers land on their dashboard; everyone else on the event list.
    private IActionResult RedirectToHome() =>
        HttpContext.Session.IsOrganiser()
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
}
