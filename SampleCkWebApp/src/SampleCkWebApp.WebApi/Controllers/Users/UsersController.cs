using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using SampleCkWebApp.Application.Users.Interfaces.Application;
using SampleCkWebApp.WebApi.Controllers;
using SampleCkWebApp.Contracts.DTOs.User;
using Contracts.DTOs.User;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace SampleCkWebApp.WebApi.Controllers.Users;

/// <summary>
/// Controller for managing users in the expense tracker system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ApiControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    
    public UsersController(IUserService userService, IAuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out userId);
    }

    private bool IsAdmin()
    {
        return User.IsInRole(Domain.Enums.Role.Admin.ToString());
    }

    private bool CanAccessUser(int userId)
    {
        return IsAdmin() || (TryGetCurrentUserId(out var currentUserId) && currentUserId == userId);
    }
    
    /// <summary>
    /// Retrieves all users from the system
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all users</returns>
    /// <response code="200">Successfully retrieved users</response>
    /// <response code="500">Internal server error</response>,
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var result = await _userService.GetAllUsersAsync(cancellationToken);
        
        return result.Match(
            users => Ok(users.ToList()),  //  Convert to response
            Problem);  
    }


    
    /// <summary>
    /// Retrieves the current authenticated user's information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current user's information</returns>
    /// <response code="200">Successfully retrieved current user</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        // Get the user ID from the JWT token claims
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { message = "Invalid or missing user ID in token" });

        var result = await _userService.GetUserByIdAsync(userId, cancellationToken);
        
        return result.Match(
            user => Ok(user),
            Problem);
    }
    
    /// <summary>
    /// Retrieves a specific user by their ID
    /// </summary>
    /// <param name="id">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user information</returns>
    /// <response code="200">User found and returned</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserById(
        [FromRoute, Required] int id, 
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(id))
            return Forbid();

        var result = await _userService.GetUserByIdAsync(id, cancellationToken);
        
        return result.Match(
            user => Ok(user),
            Problem);
    }
    
    /// <summary>
    /// Registers a new user in the system
    /// </summary>
    /// <param name="request">User registration request containing name, email, password, and currency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication response with token and user information</returns>
    /// <response code="201">User successfully registered</response>
    /// <response code="400">Validation error (invalid name, email, password, or currency)</response>
    /// <response code="409">User with this email already exists</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(
        [FromBody, Required] RegisterUserDto request, 
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        
        return result.Match(
            authResponse => CreatedAtAction(nameof(GetUserById), new { id = authResponse.User.Id }, authResponse),  //  Return authResponse (includes token)
            Problem);
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token
    /// </summary>
    /// <param name="request">Login request containing email and password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication response with token and user information</returns>
    /// <response code="200">User successfully authenticated</response>
    /// <response code="400">Validation error (invalid email or password format)</response>
    /// <response code="401">Invalid email or password</response>
    /// <response code="429">Too many login attempts - account locked</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("login")]  
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(
        [FromBody, Required] LoginUserDto request, 
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        
        return result.Match(
            authResponse => Ok(authResponse),  //  Return 200 OK (not 201 Created)
            Problem);
    }

    /// <summary>
    /// Changes the password for the current authenticated user
    /// </summary>
    /// <param name="request">Change password request containing old and new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">Password successfully changed</response>
    /// <response code="400">Validation error (invalid passwords)</response>
    /// <response code="401">Current password is incorrect</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword(
        [FromBody, Required] ChangePasswordDto request,
        CancellationToken cancellationToken)
    {
        // Get the user ID from the JWT token claims
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { message = "Invalid or missing user ID in token" });

        var result = await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        
        return result.Match(
            _ => Ok(new { message = "Password successfully changed" }),
            Problem);
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    /// <param name="id">The unique identifier of the user</param>
    /// <param name="request">User update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated user</returns>
    /// <response code="200">User successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">User not found</response>
    /// <response code="409">Email already exists for another user</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUser(
        [FromRoute, Required] int id,
        [FromBody, Required] UpdateUserDto request,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(id))
            return Forbid();

        if (!IsAdmin())
            request.Role = null;

        var result = await _userService.UpdateUserAsync(id, request, cancellationToken);
        
        return result.Match(
            user => Ok(user),
            Problem);
    }

    /// <summary>
    /// Deletes a user from the system
    /// </summary>
    /// <param name="id">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">User successfully deleted</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser(
        [FromRoute, Required] int id,
        CancellationToken cancellationToken)
    {
        var result = await _userService.DeleteUserAsync(id, cancellationToken);
        
        return result.Match(
            _ => NoContent(),  //  Returns 204 No Content on success
            Problem);
    }
}
