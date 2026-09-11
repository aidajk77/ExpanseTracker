using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using SampleCkWebApp.Application.Savings.Interfaces.Application;
using SampleCkWebApp.WebApi.Controllers;
using Contracts.DTOs.Saving;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Domain.Enums;

namespace SampleCkWebApp.WebApi.Controllers.Savings;

/// <summary>
/// Controller for managing savings in the expense tracker system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SavingsController : ApiControllerBase
{
    private readonly ISavingService _savingService;
    
    public SavingsController(ISavingService savingService)
    {
        _savingService = savingService;
    }

    private bool CanAccessUser(int userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return User.IsInRole(Role.Admin.ToString()) ||
               (int.TryParse(userIdClaim?.Value, out var currentUserId) && currentUserId == userId);
    }
    
    /// <summary>
    /// Retrieves all savings from the system
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all savings</returns>
    /// <response code="200">Successfully retrieved savings</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SavingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSavings(CancellationToken cancellationToken)
    {
        var result = await _savingService.GetAllSavingsAsync(cancellationToken);
        
        return result.Match(
            savings => Ok(savings.ToList()),  //  Convert to response
            Problem);  
    }
    
    /// <summary>
    /// Retrieves savings for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of savings the user is a part of</returns>
    /// <response code="200">Successfully retrieved user savings</response>
    /// <response code="404">User not found or has no savings</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(IEnumerable<SavingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserSavings(
        [FromRoute, Required] int userId,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _savingService.GetUserSavingsAsync(userId, cancellationToken);
        
        return result.Match(
            savings => Ok(savings.ToList()),
            Problem);
    }
    
    /// <summary>
    /// Retrieves a specific saving by its ID
    /// </summary>
    /// <param name="id">The unique identifier of the saving</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saving information</returns>
    /// <response code="200">Saving found and returned</response>
    /// <response code="404">Saving not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SavingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSavingById(
        [FromRoute, Required] int id, 
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");

        var result = await _savingService.GetSavingByIdAsync(id, userId, cancellationToken);
        
        return result.Match(
            saving => Ok(saving),
            Problem);
    }

    /// <summary>
    /// Retrieves non-completed (active) savings for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active/non-completed savings</returns>
    /// <response code="200">Successfully retrieved non-completed savings</response>
    /// <response code="404">User not found or has no non-completed savings</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/non-completed")]
    [ProducesResponseType(typeof(IEnumerable<SavingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserNonCompletedSavings(
        [FromRoute, Required] int userId,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _savingService.GetUserNonCompletedSavingsAsync(userId, cancellationToken);
        
        return result.Match(
            savings => Ok(savings.ToList()),
            Problem);
    }
    
    /// <summary>
    /// Creates a new saving in the system
    /// </summary>
    /// <param name="request">Saving creation request containing amount, goal, and other details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The newly created saving</returns>
    /// <response code="201">Saving successfully created</response>
    /// <response code="400">Validation error (invalid amount, goal, or other fields)</response>
    /// <response code="404">User or category not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(SavingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateSaving(
        [FromBody, Required] CreateSavingDto request, 
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");

        var result = await _savingService.CreateSavingAsync(request, userId, cancellationToken);
        
        return result.Match(
            saving => CreatedAtAction(nameof(GetSavingById), new { id = saving.Id }, saving),  //  Return created saving
            Problem);
    }

    /// <summary>
    /// Updates an existing saving
    /// </summary>
    /// <param name="id">The unique identifier of the saving</param>
    /// <param name="request">Saving update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated saving</returns>
    /// <response code="200">Saving successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Saving not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(SavingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSaving(
        [FromRoute, Required] int id,
        [FromBody, Required] UpdateSavingDto request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");

        var result = await _savingService.UpdateSavingAsync(id, request, userId, cancellationToken);
        
        return result.Match(
            saving => Ok(saving),
            Problem);
    }

    /// <summary>
    /// Deletes a saving from the system
    /// </summary>
    /// <param name="id">The unique identifier of the saving</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Saving successfully deleted</response>
    /// <response code="404">Saving not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSaving(
        [FromRoute, Required] int id,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");
 
        var result = await _savingService.DeleteSavingAsync(id, userId, cancellationToken);
        
        return result.Match(
            _ => NoContent(),  //  Returns 204 No Content on success
            Problem);
    }
}
