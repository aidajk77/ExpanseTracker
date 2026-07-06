using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using SampleCkWebApp.WebApi.Controllers;
using SampleCkWebApp.Application.Transaction.Interfaces.Application;
using Contracts.DTOs.Transaction;
using Microsoft.AspNetCore.Authorization;
using SampleCkWebApp.Contracts.DTOs.Common;
using Domain.Enums;
using SampleCkWebApp.Application.Category.Interfaces.Application;
using System.Security.Claims;

namespace SampleCkWebApp.WebApi.Controllers.Transactions;

/// <summary>
/// Controller for managing transactions in the expense tracker system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TransactionsController : ApiControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ICategoryService _categoryService;
    
    public TransactionsController(ITransactionService transactionService, ICategoryService categoryService)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
    }

    private bool TryGetCurrentUserId(out int currentUserId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out currentUserId);
    }

    private bool CanAccessUser(int userId)
    {
        return User.IsInRole(Role.Admin.ToString()) ||
               (TryGetCurrentUserId(out var currentUserId) && currentUserId == userId);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponse<TransactionDto>>> GetTransactionsPaginated(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _transactionService.GetPaginatedTransactionsAsync(page, limit, cancellationToken);
        return result.Match(
            transactions => Ok(transactions),
            errors => Problem(detail: errors.First().Description));
    }

    /// <summary>
    /// Retrieves paginated transactions for a specific user with optional filters
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="limit">Items per page (default: 10, max: 100)</param>
    /// <param name="type">Filter by transaction type (0=Income, 1=Expense, 2=Saving)</param>
    /// <param name="categoryId">Filter by category ID</param>
    /// <param name="savingId">Filter by saving goal ID</param>
    /// <param name="startDate">Filter by start date (yyyy-MM-dd)</param>
    /// <param name="endDate">Filter by end date (yyyy-MM-dd)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of user transactions</returns>
    /// <response code="200">Successfully retrieved user transactions</response>
    /// <response code="400">Invalid pagination parameters or filters</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/paginated")]
    [ProducesResponseType(typeof(PaginatedResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponse<TransactionDto>>> GetUserTransactionsPaginated(
        [FromRoute, Required] int userId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] TransactionType? type = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] int? savingId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _transactionService.GetUserTransactionsPaginatedAsync(
            userId, 
            page, 
            limit,
            type,
            categoryId,
            savingId,
            startDate,
            endDate,
            cancellationToken);
            
        return result.Match(
            transactions => Ok(transactions),
            errors => Problem(detail: errors.First().Description));
    }


    /// <summary>
    /// Retrieves all transactions for a specific user without pagination
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All user transactions ordered by date</returns>
    /// <response code="200">Successfully retrieved all user transactions</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/all")]
    [ProducesResponseType(typeof(IEnumerable<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllUserTransactions(
        [FromRoute, Required] int userId,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        var result = await _transactionService.GetAllUserTransactionsAsync(userId, cancellationToken);
        
        return result.Match(
            transactions => Ok(transactions),
            errors => Problem(detail: errors.First().Description));
    }
    
    /// <summary>
    /// Retrieves a specific transaction by its ID
    /// </summary>
    /// <param name="id">The unique identifier of the transaction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The transaction information</returns>
    /// <response code="200">Transaction found and returned</response>
    /// <response code="404">Transaction not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTransactionById(
        [FromRoute, Required] int id, 
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.GetTransactionByIdAsync(id, cancellationToken);
        
        return result.Match(
            transaction => Ok(transaction),
            Problem);
    }

    /// <summary>
    /// Retrieves the total monthly income for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="month">The month (1-12)</param>
    /// <param name="year">The year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The total income for the specified month</returns>
    /// <response code="200">Successfully retrieved monthly income</response>
    /// <response code="400">Invalid month or year</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/income/monthly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserMonthlyIncome(
        [FromRoute, Required] int userId,
        [FromQuery, Required] int month,
        [FromQuery, Required] int year,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        //  Validate month
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        //  Validate year
        if (year < 1900 || year > DateTime.UtcNow.Year + 10)
            return BadRequest(new { error = "Year is invalid" });

        var result = await _transactionService.GetUserMonthlyIncomeAsync(userId, month, year, cancellationToken);

        return result.Match(
            income => Ok(new { monthlyIncome = income, month, year }),
            errors => Problem(detail: errors.First().Description));
    }

    /// <summary>
    /// Retrieves total income for a user within a date range
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="startDate">Start date (yyyy-MM-dd)</param>
    /// <param name="endDate">End date (yyyy-MM-dd)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total income within the date range</returns>
    /// <response code="200">Successfully retrieved income</response>
    /// <response code="400">Invalid date range</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/income/range")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserIncomeByDateRange(
        [FromRoute, Required] int userId,
        [FromQuery, Required] DateTime startDate,
        [FromQuery, Required] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        if (startDate > endDate)
            return BadRequest(new { error = "Start date must be before end date" });

        var result = await _transactionService.GetUserIncomeByDateRangeAsync(userId, startDate, endDate, cancellationToken);

        return result.Match(
            income => Ok(new { income, startDate = startDate.Date, endDate = endDate.Date }),
            errors => Problem(detail: errors.First().Description));
    }

    /// <summary>
    /// Retrieves the total monthly expenses for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="month">The month (1-12)</param>
    /// <param name="year">The year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The total expenses for the specified month</returns>
    /// <response code="200">Successfully retrieved monthly expenses</response>
    /// <response code="400">Invalid month or year</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/expense/monthly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserMonthlyExpense(
        [FromRoute, Required] int userId,
        [FromQuery, Required] int month,
        [FromQuery, Required] int year,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        //  Validate month
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        //  Validate year
        if (year < 1900 || year > DateTime.UtcNow.Year + 10)
            return BadRequest(new { error = "Year is invalid" });

        var result = await _transactionService.GetUserMonthlyExpenseAsync(userId, month, year, cancellationToken);

        return result.Match(
            expense => Ok(new { monthlyExpense = expense, month, year }),
            errors => Problem(detail: errors.First().Description));
    }

    /// <summary>
    /// Retrieves total expenses for a user within a date range
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="startDate">Start date (yyyy-MM-dd)</param>
    /// <param name="endDate">End date (yyyy-MM-dd)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total expenses within the date range</returns>
    /// <response code="200">Successfully retrieved expenses</response>
    /// <response code="400">Invalid date range</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/expense/range")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserExpensesByDateRange(
        [FromRoute, Required] int userId,
        [FromQuery, Required] DateTime startDate,
        [FromQuery, Required] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        if (startDate > endDate)
            return BadRequest(new { error = "Start date must be before end date" });

        var result = await _transactionService.GetUserExpensesByDateRangeAsync(userId, startDate, endDate, cancellationToken);

        return result.Match(
            expenses => Ok(new { expenses, startDate = startDate.Date, endDate = endDate.Date }),
            errors => Problem(detail: errors.First().Description));
    }

    /// <summary>
    /// Retrieves the total monthly savings for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="month">The month (1-12)</param>
    /// <param name="year">The year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The total savings for the specified month</returns>
    /// <response code="200">Successfully retrieved monthly savings</response>
    /// <response code="400">Invalid month or year</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/savings/monthly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserMonthlySavings(
        [FromRoute, Required] int userId,
        [FromQuery, Required] int month,
        [FromQuery, Required] int year,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        //  Validate month
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        //  Validate year
        if (year < 1900 || year > DateTime.UtcNow.Year + 10)
            return BadRequest(new { error = "Year is invalid" });

        var result = await _transactionService.GetUserMonthlySavingsAsync(userId, month, year, cancellationToken);

        return result.Match(
            savings => Ok(new { monthlySavings = savings, month, year }),
            errors => Problem(detail: errors.First().Description));
    }
    
    /// <summary>
    /// Retrieves total savings for a user within a date range
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="startDate">Start date (yyyy-MM-dd)</param>
    /// <param name="endDate">End date (yyyy-MM-dd)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total savings within the date range</returns>
    /// <response code="200">Successfully retrieved savings</response>
    /// <response code="400">Invalid date range</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpGet("user/{userId}/savings/range")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserSavingsByDateRange(
        [FromRoute, Required] int userId,
        [FromQuery, Required] DateTime startDate,
        [FromQuery, Required] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        if (startDate > endDate)
            return BadRequest(new { error = "Start date must be before end date" });

        var result = await _transactionService.GetUserSavingsByDateRangeAsync(userId, startDate, endDate, cancellationToken);

        return result.Match(
            savings => Ok(new { savings, startDate = startDate.Date, endDate = endDate.Date }),
            errors => Problem(detail: errors.First().Description));
    }

    
    /// <summary>
    /// Creates a new transaction in the system
    /// </summary>
    /// <param name="request">Transaction creation request containing amount, category, payment method, and other details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The newly created transaction</returns>
    /// <response code="201">Transaction successfully created</response>
    /// <response code="400">Validation error (invalid amount, category, payment method, or other fields)</response>
    /// <response code="404">User, category, or payment method not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateTransaction(
        [FromBody, Required] CreateTransactionDto request, 
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.CreateTransactionAsync(request, cancellationToken);
        
        return result.Match(
            transaction => CreatedAtAction(nameof(GetTransactionById), new { id = transaction.Id }, transaction),  //  Return created transaction
            Problem);
    }

/*
    /// <summary>
    /// Updates an existing transaction
    /// </summary>
    /// <param name="id">The unique identifier of the transaction</param>
    /// <param name="request">Transaction update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated transaction</returns>
    /// <response code="200">Transaction successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Transaction not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateTransaction(
        [FromRoute, Required] int id,
        [FromBody, Required] UpdateTransactionDto request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.UpdateTransactionAsync(id, request, cancellationToken);
        
        return result.Match(
            transaction => Ok(transaction),
            Problem);
    }

*/
    /// <summary>
    /// Deletes a transaction from the system
    /// </summary>
    /// <param name="id">The unique identifier of the transaction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Transaction successfully deleted</response>
    /// <response code="404">Transaction not found</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteTransaction(
        [FromRoute, Required] int id,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.DeleteTransactionAsync(id, cancellationToken);
        
        return result.Match(
            _ => NoContent(),  //  Returns 204 No Content on success
            Problem);
    }

    /// <summary>
    /// Extracts transaction data from an uploaded document or receipt image
    /// </summary>
    /// <param name="file">Uploaded image file containing transaction document or receipt</param>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted transaction draft data for user confirmation</returns>
    /// <response code="200">Successfully extracted transaction data from document</response>
    /// <response code="400">Invalid file, unsupported image type, or extraction failed</response>
    /// <response code="401">User is not authorized</response>
    /// <response code="500">Internal server error</response>
    [Authorize]
    [HttpPost("document")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ExtractTransactionFromDocument(
        [FromForm] IFormFile? file,
        [FromForm, Required] int userId,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessUser(userId))
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("Image file is required.");

        var supportedContentTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        if (!supportedContentTypes.Contains(file.ContentType))
            return BadRequest("Only JPEG, PNG, and WEBP images are supported.");

        var categoriesResult = await _categoryService.GetAllUserCategoriesAsync(
            userId,
            cancellationToken);

        if (categoriesResult.IsError)
            return Problem(detail: categoriesResult.Errors.First().Description);

        var categories = categoriesResult.Value.ToList();
        var categoryNames = categories.Select(category => category.Name);

        var extractionResult = await _transactionService.ExtractTransactionFromImageAsync(
            file.OpenReadStream(),
            file.ContentType,
            categoryNames,
            cancellationToken);

        return extractionResult.Match<ActionResult>(
            extracted =>
            {
                var matchedCategory = categories.FirstOrDefault(category =>
                    !string.IsNullOrWhiteSpace(extracted.Category) &&
                    category.Name.Equals(extracted.Category, StringComparison.OrdinalIgnoreCase));

                var transactionType = extracted.Type?.ToLowerInvariant() switch
                {
                    "income" => TransactionType.INCOME,
                    "expense" => TransactionType.EXPENSE,
                    _ => (TransactionType?)null
                };

                return Ok(new
                {
                    UserId = userId,
                    Type = transactionType,
                    Amount = extracted.Amount,
                    CategoryId = matchedCategory?.Id,
                    CategoryName = matchedCategory?.Name ?? extracted.Category,
                    PaymentMethodId = (int?)null,
                    SavingId = (int?)null,
                    Description = extracted.Description,
                    Date = extracted.Date ?? DateTime.UtcNow
                });
            },
        errors =>
        {
            var error = errors.First();

            Console.WriteLine($"[DOCUMENT_SCAN_ERROR] Code: {error.Code}");
            Console.WriteLine($"[DOCUMENT_SCAN_ERROR] Description: {error.Description}");

            return BadRequest(new
            {
                code = error.Code,
                message = error.Description
            });
        });
    }
}
