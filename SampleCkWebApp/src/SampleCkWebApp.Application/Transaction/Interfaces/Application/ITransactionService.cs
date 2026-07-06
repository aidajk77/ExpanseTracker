using ErrorOr;
using Contracts.DTOs.Transaction;
using SampleCkWebApp.Contracts.DTOs.Common;
using Domain.Enums;
using SampleCkWebApp.Contracts.DTOs.Transaction;

namespace SampleCkWebApp.Application.Transaction.Interfaces.Application;

public interface ITransactionService
{
    Task<ErrorOr<PaginatedResponse<TransactionDto>>> GetPaginatedTransactionsAsync(
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<PaginatedResponse<TransactionDto>>> GetUserTransactionsPaginatedAsync(
        int userId,
        int page = 1,
        int limit = 10,
        TransactionType? type = null,
        int? categoryId = null,
        int? savingId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<decimal>> GetUserMonthlyIncomeAsync(
        int userId,
        int month,
        int year,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<decimal>> GetUserIncomeByDateRangeAsync(
        int userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<decimal>> GetUserMonthlyExpenseAsync(
        int userId,
        int month,
        int year,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<decimal>> GetUserExpensesByDateRangeAsync(
        int userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
    
    Task<ErrorOr<decimal>> GetUserMonthlySavingsAsync(
        int userId,
        int month,
        int year,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<decimal>> GetUserSavingsByDateRangeAsync(
        int userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<IEnumerable<TransactionDto>>> GetAllUserTransactionsAsync(
        int userId,
        CancellationToken cancellationToken = default);
    Task<ErrorOr<TransactionDto>> GetTransactionByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ErrorOr<TransactionDto>> CreateTransactionAsync(CreateTransactionDto request, CancellationToken cancellationToken = default);
    //Task<ErrorOr<TransactionDto>> UpdateTransactionAsync(int id, UpdateTransactionDto request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Success>> DeleteTransactionAsync(int id, CancellationToken cancellationToken = default);
    Task<ErrorOr<MlTransactionExtractionResult>> ExtractTransactionFromImageAsync(
        Stream imageStream,
        string contentType,
        IEnumerable<string> availableCategories,
        CancellationToken cancellationToken = default);
}
