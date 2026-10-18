using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Models.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

// The logged-in user's own profile. Available to both roles.
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UsersController(RaceDayDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    // Returns the profile of whoever is logged in.
    // <response code="200">The profile.</response>
    // <response code="401">Nobody is logged in.</response>
    [HttpGet("me")]
    [RequireRole]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetMyProfile()
    {
        var userId = HttpContext.Session.GetUserId()!.Value;

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new { message = "This account no longer exists." });
        }

        return Ok(AuthController.ToProfile(user, user.Role!.RoleName));
    }

    // Updates the logged-in user's own personal details. A user can only
    // ever change their own profile, because the id comes from the session
    // rather than from the request.
    // <response code="200">The updated profile.</response>
    // <response code="400">Validation failed.</response>
    // <response code="401">Nobody is logged in.</response>
    [HttpPut("me")]
    [RequireRole]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateMyProfile(UpdateProfileRequest request)
    {
        var userId = HttpContext.Session.GetUserId()!.Value;

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new { message = "This account no longer exists." });
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber;
        user.DateOfBirth = request.DateOfBirth;
        user.Gender = request.Gender;
        user.City = request.City;

        await _db.SaveChangesAsync();

        return Ok(AuthController.ToProfile(user, user.Role!.RoleName));
    }

    // Changes the logged-in user's password, after confirming the current one.
    // <response code="204">The password was changed.</response>
    // <response code="400">The current password was wrong, or the new one is too weak.</response>
    // <response code="401">Nobody is logged in.</response>
    [HttpPut("me/password")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = HttpContext.Session.GetUserId()!.Value;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new { message = "This account no longer exists." });
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.CurrentPassword);

        if (verification == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "The current password is incorrect." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}