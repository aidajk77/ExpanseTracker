using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using SampleCkWebApp.WebApi.Controllers;
using SampleCkWebApp.Application.Budgets.Interfaces.Application;
using Contracts.DTOs.Budget;
using Microsoft.AspNetCore.Authorization;
using SampleCkWebApp.Contracts.DTOs.Budget;
using System.Security.Claims;
using Domain.Enums;

namespace SampleCkWebApp.WebApi.Controllers.Budgets;

/// <summary>
/// Controller for managing budgets in the expense tracker system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BudgetsController : ApiControllerBase
{
    private readonly IBudgetService _budgetService;
    
    public BudgetsController(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    private bool CanAccessUser(int userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return User.IsInRole(Role.Admin.ToString()) ||
               (int.TryParse(userIdClaim?.Value, out var currentUserId) && currentUserId == userId);
    }
    
    /// <summary>
    /// Retrieves all budgets from the system
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all budgets</returns>
    /// <response code="200">Successfully retrieved budgets</response>
    /// <response code="500">Internal server error</response>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BudgetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBudgets(CancellationToken cancellationToken)
    {
        var result = await _budgetService.GetAllBudgetsAsync(cancellationToken);
        
        return result.Match(
            budgets => Ok(budgets.ToList()),  //  Convert to response
            Problem);  
    }
    
    /// <summary>
    /// Retrieves a specific budget by its ID
    /// </summary>
    /// <param name="id">The unique identifier of the budget</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The budget information</returns>
    /// <response code="200">Budget found and returned</response>
    /// <response code="404">Budget not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBudgetById(
        [FromRoute, Required] int id, 
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");
        var result = await _budgetService.GetBudgetByIdAsync(id, userId, cancellationToken);
        
        return result.Match(
            budget => Ok(budget),
            Problem);
    }

     /// <summary>
    /// Retrieves all budgets for a specific user (through their categories)
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of budgets for the user's categories</returns>
    /// <response code="200">Successfully retrieved user budgets</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(IEnumerable<BudgetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserBudgets(
        [FromRoute, Required] int userId,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _budgetService.GetUserBudgetsAsync(userId, cancellationToken);
        
        return result.Match(
            budgets => Ok(budgets.ToList()),
            Problem);
    }
    
    /// <summary>
    /// Retrieves all budgets for a specific user for a specific month
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="month">The month (1-12)</param>
    /// <param name="year">The year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of budgets for the user's categories in the specified month</returns>
    /// <response code="200">Successfully retrieved user budgets</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/month/{month}/year/{year}")]
    [ProducesResponseType(typeof(IEnumerable<BudgetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserBudgetsForMonth(
        [FromRoute, Required] int userId,
        [FromRoute, Required] int month,
        [FromRoute, Required] int year,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _budgetService.GetUserBudgetsForMonthAsync(userId, month, year, cancellationToken);
        
        return result.Match(
            budgets => Ok(budgets.ToList()),
            Problem);
    }

    /// <summary>
    /// Get budget summary for a user (total budgeted, spent, remaining, percentage)
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Budget summary with totals and percentages</returns>
    /// <response code="200">Successfully retrieved budget summary</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/summary")]
    [ProducesResponseType(typeof(BudgetSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserBudgetSummary(
        [FromRoute, Required] int userId,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _budgetService.GetUserBudgetSummaryAsync(userId, cancellationToken);
        
        return result.Match(
            summary => Ok(summary),
            Problem);
    }

    /// <summary>
    /// Get budget summary for a user for a specific month
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="month">The month (1-12)</param>
    /// <param name="year">The year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Budget summary for the specific month</returns>
    /// <response code="200">Successfully retrieved budget summary</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/month/{month}/year/{year}/summary")]
    [ProducesResponseType(typeof(BudgetSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserBudgetSummaryForMonth(
        [FromRoute, Required] int userId,
        [FromRoute, Required] int month,
        [FromRoute, Required] int year,
        CancellationToken cancellationToken)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _budgetService.GetUserBudgetSummaryForMonthAsync(userId, month, year, cancellationToken);
        
        return result.Match(
            summary => Ok(summary),
            Problem);
    }

    
    /// <summary>
    /// Creates a new budget in the system
    /// </summary>
    /// <param name="request">Budget creation request containing name, amount, category, and time period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The newly created budget</returns>
    /// <response code="201">Budget successfully created</response>
    /// <response code="400">Validation error (invalid name, amount, category, or time period)</response>
    /// <response code="404">User or category not found</response>
    /// <response code="409">Budget already exists for this category in this period</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(BudgetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBudget(
        [FromBody, Required] CreateBudgetDto request, 
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");
        var result = await _budgetService.CreateBudgetAsync(request,userId, cancellationToken);
        
        return result.Match(
            budget => CreatedAtAction(nameof(GetBudgetById), new { id = budget.Id }, budget),  //  Return created budget
            Problem);
    }

    /// <summary>
    /// Updates an existing budget
    /// </summary>
    /// <param name="id">The unique identifier of the budget</param>
    /// <param name="request">Budget update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated budget</returns>
    /// <response code="200">Budget successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Budget not found</response>
    /// <response code="409">Budget already exists for this category in this period</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(BudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateBudget(
        [FromRoute, Required] int id,
        [FromBody, Required] UpdateBudgetDto request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");
        var result = await _budgetService.UpdateBudgetAsync(id, userId, request, cancellationToken);
        
        return result.Match(
            budget => Ok(budget),
            Problem);
    }

    /// <summary>
    /// Deletes a budget from the system
    /// </summary>
    /// <param name="id">The unique identifier of the budget</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Budget successfully deleted</response>
    /// <response code="404">Budget not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteBudget(
        [FromRoute, Required] int id,
        CancellationToken cancellationToken)
    {
        var result = await _budgetService.DeleteBudgetAsync(id, cancellationToken);
        
        return result.Match(
            _ => NoContent(),  //  Returns 204 No Content on success
            Problem);
    }
}
