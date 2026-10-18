using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;
using Xunit;

namespace RaceDay.Api.Tests;

// Covers registration and login, in both the success and the failure cases.
public class AuthControllerTests
{
    private const string InviteCode = "TEST-ORG-CODE";

    private static AuthController BuildController(RaceDayDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RaceDay:OrganiserInviteCode"] = InviteCode
            })
            .Build();

        var controller = new AuthController(db, new PasswordHasher<User>(), configuration);
        TestDatabase.SignIn(controller, null, null);
        return controller;
    }

    [Fact]
    public async Task Register_WithValidParticipantDetails_CreatesTheAccount()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);

        var result = await controller.Register(new RegisterRequest
        {
            FirstName = "New",
            LastName = "Runner",
            Email = "new.runner@test.local",
            Password = "Str0ngPass!2026",
            Role = RoleNames.Participant
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var profile = Assert.IsType<UserProfileResponse>(created.Value);

        Assert.Equal(RoleNames.Participant, profile.Role);
        Assert.Equal("new.runner@test.local", profile.Email);
    }

    [Fact]
    public async Task Register_StoresAHashedPassword_NotThePlainText()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);

        const string plainPassword = "Str0ngPass!2026";

        await controller.Register(new RegisterRequest
        {
            FirstName = "New",
            LastName = "Runner",
            Email = "hashed@test.local",
            Password = plainPassword,
            Role = RoleNames.Participant
        });

        var saved = db.Users.First(u => u.Email == "hashed@test.local");

        Assert.NotEqual(plainPassword, saved.PasswordHash);
        Assert.DoesNotContain(plainPassword, saved.PasswordHash);

        var verification = new PasswordHasher<User>()
            .VerifyHashedPassword(saved, saved.PasswordHash, plainPassword);
        Assert.Equal(PasswordVerificationResult.Success, verification);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);

        var result = await controller.Register(new RegisterRequest
        {
            FirstName = "Copy",
            LastName = "Cat",
            Email = "participant@test.local",
            Password = "Str0ngPass!2026",
            Role = RoleNames.Participant
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Register_AsOrganiser_WithoutTheInviteCode_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);

        var result = await controller.Register(new RegisterRequest
        {
            FirstName = "Would Be",
            LastName = "Organiser",
            Email = "wouldbe@test.local",
            Password = "Str0ngPass!2026",
            Role = RoleNames.Organiser
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.Users.Where(u => u.Email == "wouldbe@test.local"));
    }

    [Fact]
    public async Task Register_AsOrganiser_WithTheInviteCode_Succeeds()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);

        var result = await controller.Register(new RegisterRequest
        {
            FirstName = "Real",
            LastName = "Organiser",
            Email = "real.organiser@test.local",
            Password = "Str0ngPass!2026",
            Role = RoleNames.Organiser,
            OrganiserInviteCode = InviteCode
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var profile = Assert.IsType<UserProfileResponse>(created.Value);
        Assert.Equal(RoleNames.Organiser, profile.Role);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_StartsASessionHoldingTheUserAndRole()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);
        var participant = TestDatabase.Participant(db);

        var result = await controller.Login(new LoginRequest
        {
            Email = participant.Email,
            Password = TestDatabase.Password
        });

        Assert.IsType<OkObjectResult>(result.Result);

        var session = controller.HttpContext.Session;
        Assert.Equal(participant.UserId, session.GetUserId());
        Assert.Equal(RoleNames.Participant, session.GetRole());
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsRejectedAndStartsNoSession()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);
        var participant = TestDatabase.Participant(db);

        var result = await controller.Login(new LoginRequest
        {
            Email = participant.Email,
            Password = "TheWrongPassword!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Null(controller.HttpContext.Session.GetUserId());
    }

    [Fact]
    public async Task Login_WithUnknownEmail_IsRejected()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);

        var result = await controller.Login(new LoginRequest
        {
            Email = "nobody@test.local",
            Password = TestDatabase.Password
        });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public void Logout_ClearsTheSession()
    {
        using var db = TestDatabase.Create();
        TestDatabase.Seed(db);
        var controller = BuildController(db);

        TestDatabase.SignIn(controller, TestDatabase.Participant(db), RoleNames.Participant);
        Assert.NotNull(controller.HttpContext.Session.GetUserId());

        controller.Logout();

        Assert.Null(controller.HttpContext.Session.GetUserId());
    }
}