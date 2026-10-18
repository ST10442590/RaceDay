using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;
using RegisterRequest = RaceDay.Api.Models.Dtos.RegisterRequest;
using LoginRequest = RaceDay.Api.Models.Dtos.LoginRequest;

namespace RaceDay.Api.Controllers;

// Registration, login and logout.
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthController(
        RaceDayDbContext db,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    // Creates a new account as either a Participant or an Organiser.
    // Participants may sign up freely. Organiser registration requires the
    // invite code, so that event management cannot be claimed by anyone.
    // The password is hashed before it is stored; it is never saved as text.
    // <response code="201">The account was created. Returns the new profile.</response>
    // <response code="400">Validation failed, or the Organiser invite code was wrong.</response>
    // <response code="409">That email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> Register(RegisterRequest request)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "Role must be either 'Organiser' or 'Participant'." });
        }

        if (role.RoleName == RoleNames.Organiser)
        {
            var expectedCode = _configuration["RaceDay:OrganiserInviteCode"];
            if (string.IsNullOrWhiteSpace(request.OrganiserInviteCode) ||
                request.OrganiserInviteCode != expectedCode)
            {
                return BadRequest(new
                {
                    message = "A valid organiser invite code is required to register as an Organiser."
                });
            }
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { message = "That email address is already registered." });
        }

        var user = new User
        {
            RoleId = role.RoleId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            City = request.City
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var profile = ToProfile(user, role.RoleName);
        return CreatedAtAction(nameof(UsersController.GetMyProfile), "Users", null, profile);
    }

    // Checks the email and password, and starts a server-side session that
    // holds the user's id and role for every later request. The browser
    // receives only an opaque session cookie.
    // <response code="200">Logged in. Returns the user's profile.</response>
    // <response code="401">The email or password is incorrect.</response>
    // <response code="403">The account has been deactivated.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserProfileResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            // Same message as a wrong password, so the response does not
            // reveal which email addresses are registered.
            return Unauthorized(new { message = "Incorrect email address or password." });
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Incorrect email address or password." });
        }

        if (!user.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "This account has been deactivated." });
        }

        HttpContext.Session.SignIn(user, user.Role!.RoleName);

        return Ok(ToProfile(user, user.Role.RoleName));
    }

    // Ends the current session and logs the user out.
    /// <response code="204">The session was cleared.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    // Reports who is currently logged in. Useful for checking in Swagger
    // that the session cookie is working.
    // <response code="200">Somebody is logged in.</response>
    // <response code="401">Nobody is logged in.</response>
    [HttpGet("session")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetSession()
    {
        var userId = HttpContext.Session.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "No active session." });
        }

        return Ok(new
        {
            userId,
            role = HttpContext.Session.GetRole(),
            name = HttpContext.Session.GetDisplayName()
        });
    }

    internal static UserProfileResponse ToProfile(User user, string roleName) => new()
    {
        UserId = user.UserId,
        Role = roleName,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        DateOfBirth = user.DateOfBirth,
        Gender = user.Gender,
        City = user.City,
        CreatedAt = user.CreatedAt
    };
}