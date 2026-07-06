using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using SampleCkWebApp.WebApi.Controllers;
using SampleCkWebApp.Application.UserSaving.Interfaces.Application;
using Contracts.DTOs.UserSaving;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Domain.Enums;

namespace SampleCkWebApp.WebApi.Controllers.UserSavings;

/// <summary>
/// Controller for managing user savings associations in the expense tracker system
/// </summary>
[ApiController]
[Route("api/users/{userId}/savings")]
[Produces("application/json")]
public class UserSavingsController : ApiControllerBase
{
    private readonly IUserSavingService _userSavingService;
    
    public UserSavingsController(IUserSavingService userSavingService)
    {
        _userSavingService = userSavingService;
    }

    private bool CanAccessUser(int userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return User.IsInRole(Role.Admin.ToString()) ||
               (int.TryParse(userIdClaim?.Value, out var currentUserId) && currentUserId == userId);
    }
    
    /// <summary>
    /// Retrieves all savings associated with a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all savings for the user</returns>
    /// <response code="200">Successfully retrieved user savings</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserSavingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserSavings(
        [FromRoute, Required] int userId, 
        CancellationToken cancellationToken)
    {
        var result = await _userSavingService.GetAllUserSavingsAsync(cancellationToken);
        
        return result.Match(
            userSavings => Ok(userSavings.ToList()),  //  Return list of user savings
            Problem);  
    }
    
    /// <summary>
    /// Retrieves a specific saving associated with a user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="savingId">The unique identifier of the saving</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user saving association details</returns>
    /// <response code="200">User saving association found and returned</response>
    /// <response code="404">User or saving not found, or association doesn't exist</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("{savingId}")]
    [ProducesResponseType(typeof(UserSavingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserSavingById(
        [FromRoute, Required] int userId,
        [FromRoute, Required] int savingId, 
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _userSavingService.GetUserSavingByIdAsync(userId, savingId, cancellationToken);
        
        return result.Match(
            userSaving => Ok(userSaving),
            Problem);
    }
    
    /// <summary>
    /// Associates a saving with a user (adds saving to user's account)
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="request">Request containing saving association details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The newly created user saving association</returns>
    /// <response code="201">Saving successfully associated with user</response>
    /// <response code="400">Validation error (invalid user, saving, or data)</response>
    /// <response code="404">User or saving not found</response>
    /// <response code="409">Saving already associated with this user</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(UserSavingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddSavingToUser(
        [FromRoute, Required] int userId,
        [FromBody, Required] CreateUserSavingDto request, 
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        request.UserId = userId;

        var result = await _userSavingService.CreateUserSavingAsync(request, cancellationToken);
        
        return result.Match(
            userSaving => CreatedAtAction(nameof(GetUserSavingById), new { userId, savingId = userSaving.SavingId }, userSaving),  //  Return created association
            Problem);
    }

    /// <summary>
    /// Updates a saving association for a user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="savingId">The unique identifier of the saving</param>
    /// <param name="request">Saving association update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated user saving association</returns>
    /// <response code="200">Saving association successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">User, saving, or association not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPut("{savingId}")]
    [ProducesResponseType(typeof(UserSavingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUserSaving(
        [FromRoute, Required] int userId,
        [FromRoute, Required] int savingId,
        [FromBody, Required] UpdateUserSavingDto request,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _userSavingService.UpdateUserSavingAsync(userId, savingId, request, cancellationToken);
        
        return result.Match(
            userSaving => Ok(userSaving),
            Problem);
    }

    /// <summary>
    /// Removes a saving association from a user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="savingId">The unique identifier of the saving</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Saving association successfully removed</response>
    /// <response code="404">User, saving, or association not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpDelete("{savingId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveSavingFromUser(
        [FromRoute, Required] int userId,
        [FromRoute, Required] int savingId,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _userSavingService.DeleteUserSavingAsync(userId, savingId, cancellationToken);
        
        return result.Match(
            _ => NoContent(),  //  Returns 204 No Content on success
            Problem);
    }
}
