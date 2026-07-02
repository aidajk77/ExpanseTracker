using ErrorOr;
using SampleCkWebApp.Application.Common.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Transaction.Interfaces.Application;
using SampleCkWebApp.Application.Category.Interfaces.Application;
using SampleCkWebApp.Application.Budgets.Interfaces.Application;
using Contracts.DTOs.Transaction;
using Domain.Entities;
using api.Mappers;
using Domain.Enums;
using Domain.Errors;
using SampleCkWebApp.Contracts.DTOs.Common;
using SampleCkWebApp.Application.Savings.Interfaces.Application;
using SampleCkWebApp.Application.Transaction.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Budget.Interfaces.Infrastructure;
using SampleCkWebApp.Application.PaymentMethod.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Users.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Savings.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Category.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Currencies.Interfaces.Application;
using SampleCkWebApp.Application.Currencies.Interfaces.Infrastructure;
using SampleCkWebApp.Contracts.DTOs.Transaction;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace SampleCkWebApp.Application.Transaction;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISavingRepository _savingRepository;
    private readonly ICategoryService _categoryService;  
    private readonly IBudgetService _budgetService;   
    private readonly ISavingService _savingService;   
    private readonly TransactionValidator _transactionValidator;
    private const int USD_CURRENCY_ID = 4;

    public TransactionService(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IPaymentMethodRepository paymentMethodRepository,
        ICurrencyRepository currencyRepository,
        IUserRepository userRepository,
        ISavingRepository savingRepository,
        ICategoryService categoryService,  
        IBudgetService budgetService,   
        ISavingService savingService,   
        TransactionValidator transactionValidator)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _userRepository = userRepository;
        _savingRepository = savingRepository;
        _categoryService = categoryService; 
        _budgetService = budgetService;    
        _savingService = savingService;  
        _currencyRepository = currencyRepository;
        _transactionValidator = transactionValidator;
    }

    /*public async Task<ErrorOr<PaginatedResponse<TransactionDto>>> GetPaginatedTransactionsAsync(
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            page = 1;

        if (limit < 1)
            limit = 10;

        if (limit > 100)
            limit = 100;

        var allTransactions = await _transactionRepository.GetAllAsync();
        var totalCount = allTransactions.Count();
        var totalPages = (int)Math.Ceiling(totalCount / (double)limit);

        var transactions = allTransactions
            .OrderByDescending(t => t.Date)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(t => t.ToDto())
            .ToList();

        var response = new PaginatedResponse<TransactionDto>
        {
            Data = transactions,
            Total = totalCount,
            Page = page,
            Limit = limit,
            TotalPages = totalPages
        };

        return response;
    }

*/
    private async Task<decimal> ConvertFromUSDToUserCurrencyAsync(
        decimal amountInUSD,
        int userCurrencyId)
    {
        // If user currency is USD, no conversion needed
        if (userCurrencyId == USD_CURRENCY_ID)
            return amountInUSD;

        // Get exchange rate from Currency table
        var userCurrency = await _currencyRepository.GetByIdAsync(userCurrencyId);
        
        if (userCurrency == null || userCurrency.ExchangeRate <= 0)
            return amountInUSD; // Fallback if currency not found

        // ExchangeRate column stores: 1 USD = X currency
        return amountInUSD * userCurrency.ExchangeRate;
    }
    private async Task<decimal> ConvertFromUserCurrencyToUSDAsync(
        decimal amountInUserCurrency,
        int userCurrencyId)
    {
        // If user currency is USD, no conversion needed
        if (userCurrencyId == USD_CURRENCY_ID)
            return amountInUserCurrency;

        // Get exchange rate from Currency table
        var userCurrency = await _currencyRepository.GetByIdAsync(userCurrencyId);
        
        if (userCurrency == null || userCurrency.ExchangeRate <= 0)
            return amountInUserCurrency; // Fallback if currency not found

        // ExchangeRate column stores: 1 USD = X currency
        // Reverse: amount_in_user_currency / exchange_rate = amount_in_usd
        return amountInUserCurrency / userCurrency.ExchangeRate;
    }

    public async Task<ErrorOr<PaginatedResponse<TransactionDto>>> GetUserTransactionsPaginatedAsync(
        int userId,
        int page = 1,
        int limit = 10,
        TransactionType? type = null,
        int? categoryId = null,
        int? savingId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;

        if (page < 1)
            page = 1;

        if (limit < 1)
            limit = 10;

        if (limit > 100)
            limit = 100;
        
        var (transactions, totalCount) = await _transactionRepository.GetUserTransactionsPagedAsync(
        userId,
        page,
        limit,
        type,
        categoryId,
        savingId,
        startDate,
        endDate);

        var totalPages = (int)Math.Ceiling(totalCount / (double)limit);

        var transactionDtos = new List<TransactionDto>();
        foreach (var transaction in transactions)
        {
            var dto = transaction.ToDto();
            
            dto.Amount = await ConvertFromUSDToUserCurrencyAsync(
                transaction.Amount,
                user.CurrencyId);
            
            transactionDtos.Add(dto);
        }

        var response = new PaginatedResponse<TransactionDto>
        {
            Data = transactionDtos,
            Total = totalCount,
            Page = page,
            Limit = limit,
            TotalPages = totalPages
        };

        return response;
    }

    public async Task<ErrorOr<IEnumerable<TransactionDto>>> GetAllUserTransactionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;

        var (userTransactions, _) = await _transactionRepository.GetUserTransactionsPagedAsync(
        userId,
        page: 1,
        limit: 10000,  // Large limit to get all at once
        type: null,
        categoryId: null,
        savingId: null,
        startDate: null,
        endDate: null);

        var transactionDtos = new List<TransactionDto>();
        foreach (var transaction in userTransactions.OrderByDescending(t => t.Date))
        {
            var dto = transaction.ToDto();
            
            dto.Amount = await ConvertFromUSDToUserCurrencyAsync(
                transaction.Amount,
                user.CurrencyId);
            
            transactionDtos.Add(dto);
        }

        return transactionDtos;
    }
    

    public async Task<ErrorOr<decimal>> GetUserMonthlyIncomeAsync(
    int userId,
    int month,
    int year,
    CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;

        var userIncomeInUSD = await _transactionRepository.GetUserMonthlyTotalAsync(
            userId,
            TransactionType.INCOME,
            month,
            year);

        var convertedIncome = await ConvertFromUSDToUserCurrencyAsync(
            userIncomeInUSD,
            user.CurrencyId);

        return convertedIncome;
    }

    public async Task<ErrorOr<decimal>> GetUserIncomeByDateRangeAsync(
    int userId,
    DateTime startDate,
    DateTime endDate,
    CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;
        
        var userIncomeInUSD = await _transactionRepository.GetUserTotalByTypeAndDateRangeAsync(
            userId,
            TransactionType.INCOME,
            startDate,
            endDate);

        var convertedIncome = await ConvertFromUSDToUserCurrencyAsync(
            userIncomeInUSD,
            user.CurrencyId);

        return convertedIncome;
    }

    public async Task<ErrorOr<decimal>> GetUserMonthlyExpenseAsync(
    int userId,
    int month,
    int year,
    CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;

        var userExpenseInUSD = await _transactionRepository.GetUserMonthlyTotalAsync(
            userId,
            TransactionType.EXPENSE,
            month,
            year);

        var convertedExpense = await ConvertFromUSDToUserCurrencyAsync(
            userExpenseInUSD,
            user.CurrencyId);

        return convertedExpense;
    }

    public async Task<ErrorOr<decimal>> GetUserExpensesByDateRangeAsync(
    int userId,
    DateTime startDate,
    DateTime endDate,
    CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;

        var userExpensesInUSD = await _transactionRepository.GetUserTotalByTypeAndDateRangeAsync(
            userId,
            TransactionType.EXPENSE,
            startDate,
            endDate);

        var convertedExpenses = await ConvertFromUSDToUserCurrencyAsync(
            userExpensesInUSD,
            user.CurrencyId);

        return convertedExpenses;
    }

    public async Task<ErrorOr<decimal>> GetUserMonthlySavingsAsync(
    int userId,
    int month,
    int year,
    CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;

        var userSavingsInUSD = await _transactionRepository.GetUserMonthlyTotalAsync(
            userId,
            TransactionType.SAVING,
            month,
            year);

        var convertedSavings = await ConvertFromUSDToUserCurrencyAsync(
            userSavingsInUSD,
            user.CurrencyId);

        return convertedSavings;
    }

    public async Task<ErrorOr<decimal>> GetUserSavingsByDateRangeAsync(
    int userId,
    DateTime startDate,
    DateTime endDate,
    CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return TransactionErrors.InvalidUser;

        var userSavingsInUSD = await _transactionRepository.GetUserTotalByTypeAndDateRangeAsync(
            userId,
            TransactionType.SAVING,
            startDate,
            endDate);

        var convertedSavings = await ConvertFromUSDToUserCurrencyAsync(
            userSavingsInUSD,
            user.CurrencyId);

        return convertedSavings;
    }

    public async Task<ErrorOr<TransactionDto>> GetTransactionByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id);
        if (transaction == null)
            return TransactionErrors.NotFound;

        return transaction.ToDto();
    }

    public async Task<ErrorOr<TransactionDto>> CreateTransactionAsync(
    CreateTransactionDto request,
    CancellationToken cancellationToken = default)
    {   
        var validationResult = _transactionValidator.ValidateCreateTransaction(request);
        if (validationResult.IsError)
            return validationResult.Errors;

        var (userExists, paymentMethodExists, categoryExists, savingExists, userInSaving) = 
        await _transactionRepository.ValidateTransactionDependenciesAsync(
            request.UserId,
            request.PaymentMethodId,
            request.CategoryId,
            request.SavingId);

        if (!userExists)
            return TransactionErrors.InvalidUser;

        if (!paymentMethodExists)
            return TransactionErrors.InvalidPaymentMethod;
        
        var user = await _userRepository.GetByIdAsync(request.UserId);
        
        var amountInUSD = await ConvertFromUserCurrencyToUSDAsync(
            request.Amount,
            user.CurrencyId);

        var transactionRequest = new CreateTransactionDto
        {
            UserId = request.UserId,
            CategoryId = request.CategoryId,
            PaymentMethodId = request.PaymentMethodId,
            SavingId = request.SavingId,
            Type = request.Type,
            Amount = amountInUSD, 
            Description = request.Description,
            Date = request.Date
        };

        //  Handle INCOME/EXPENSE with CategoryId
        if (request.Type == TransactionType.INCOME || request.Type == TransactionType.EXPENSE)
        {
            if (!request.CategoryId.HasValue || !categoryExists)
                return TransactionErrors.InvalidCategory;

            var transaction = transactionRequest.ToModel();


            var categoryResult = await _categoryService.UpdateCategoryAsync(
                request.CategoryId.Value,
                request.Type,
                amountInUSD,
                cancellationToken);

            if (categoryResult.IsError)
            {
                Console.WriteLine($"[ERROR] Failed to update category: {categoryResult.Errors.First().Description}");
                return Error.Failure("Transaction.CategoryUpdateFailed", "Failed to update category");
            }

            await _transactionRepository.AddAsync(transaction);
            await _transactionRepository.SaveChangesAsync();

            return transaction.ToDto();
        }

        //  Handle SAVING with SavingId
        else if (request.Type == TransactionType.SAVING)
        {
            if (!request.SavingId.HasValue || !savingExists)
                return SavingErrors.NotFound;

            if (!userInSaving)
                return SavingErrors.UserNotPartOfSaving;

            var transaction = transactionRequest.ToModel();


            var updateResult = await _savingService.AddSavingTransactionAsync(
                request.SavingId.Value,
                request.UserId,
                amountInUSD,
                cancellationToken);

            if (updateResult.IsError)
            {
                Console.WriteLine($"[ERROR] Failed to update saving: {updateResult.Errors.First().Description}");
                return Error.Failure("Transaction.SavingUpdateFailed", "Failed to update saving");
            }

            await _transactionRepository.AddAsync(transaction);
            await _transactionRepository.SaveChangesAsync();

            return transaction.ToDto();
        }

        return Error.Failure("Transaction.InvalidType", "Invalid transaction type");
    }

    /*public async Task<ErrorOr<TransactionDto>> UpdateTransactionAsync(
        int id,
        UpdateTransactionDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = _transactionValidator.ValidateUpdateTransaction(request);
        if (validationResult.IsError)
            return validationResult.Errors;

        var transaction = await _transactionRepository.GetByIdAsync(id);
        if (transaction == null)
            return TransactionErrors.NotFound;

        var oldCategoryId = transaction.CategoryId;
        var oldSavingId = transaction.SavingId;
        var oldAmount = transaction.Amount;
        var oldType = transaction.Type;
        var oldUserId = transaction.UserId;

        //  Update transaction fields
        if (request.CategoryId.HasValue && request.CategoryId > 0)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId.Value);
            if (category == null)
                return TransactionErrors.InvalidCategory;
            transaction.CategoryId = request.CategoryId.Value;
        }

        if (request.SavingId.HasValue && request.SavingId > 0)
        {
            var savingResult = await _savingService.GetSavingByIdAsync(request.SavingId.Value, cancellationToken);
            if (savingResult.IsError)
                return savingResult.Errors;
            
            transaction.SavingId = request.SavingId.Value;
        }

        if (request.PaymentMethodId.HasValue)
        {
            var paymentMethod = await _paymentMethodRepository.GetByIdAsync(request.PaymentMethodId.Value);
            if (paymentMethod == null)
                return TransactionErrors.InvalidPaymentMethod;
            transaction.PaymentMethodId = request.PaymentMethodId.Value;
        }

        if (request.Amount.HasValue)
            transaction.Amount = request.Amount.Value;

        if (request.Type.HasValue)
            transaction.Type = request.Type.Value;

        if (!string.IsNullOrEmpty(request.Description))
            transaction.Description = request.Description;

        if (request.Date.HasValue)
            transaction.Date = request.Date.Value;

        await _transactionRepository.UpdateAsync(transaction);
        await _transactionRepository.SaveChangesAsync();

        //  Handle updates based on OLD type - REVERT old values
        if (oldType == TransactionType.INCOME || oldType == TransactionType.EXPENSE)
        {
            //  Revert old amount from old category
            await _categoryService.UpdateCategoryAsync(
                oldCategoryId.Value,
                oldType,
                -oldAmount,
                cancellationToken);

            Console.WriteLine($"[UPDATE] Removed {oldType} from category {oldCategoryId}: -{oldAmount}");
        }
        else if (oldType == TransactionType.SAVING)
        {
            //  Remove old saving transaction through service
            if (oldSavingId.HasValue)
            {
                var removeResult = await _savingService.RemoveSavingTransactionAsync(
                    oldSavingId.Value,
                    oldUserId,
                    oldAmount,
                    cancellationToken);

                if (removeResult.IsError)
                    Console.WriteLine($"[ERROR] Failed to remove old saving: {removeResult.Errors.First().Description}");

                Console.WriteLine($"[UPDATE] Removed {oldAmount} from saving {oldSavingId}");
            }
        }

        //  Handle updates based on NEW type - APPLY new values
        if (transaction.Type == TransactionType.INCOME || transaction.Type == TransactionType.EXPENSE)
        {
            //  Add new amount to new category
            var categoryResult = await _categoryService.UpdateCategoryAsync(
                transaction.CategoryId.Value,
                transaction.Type,
                transaction.Amount,
                cancellationToken);

            if (categoryResult.IsError)
            {
                Console.WriteLine($"[ERROR] Failed to update category: {categoryResult.Errors.First().Description}");
                return Error.Failure("Transaction.CategoryUpdateFailed", "Failed to update category");
            }

            Console.WriteLine($"[UPDATE] Added {transaction.Type} to category {transaction.CategoryId}: +{transaction.Amount}");
        }
        else if (transaction.Type == TransactionType.SAVING)
        {
            //  Add new saving transaction through service
            if (transaction.SavingId.HasValue)
            {
                var addResult = await _savingService.AddSavingTransactionAsync(
                    transaction.SavingId.Value,
                    transaction.UserId,
                    transaction.Amount,
                    cancellationToken);

                if (addResult.IsError)
                {
                    Console.WriteLine($"[ERROR] Failed to update saving: {addResult.Errors.First().Description}");
                    return Error.Failure("Transaction.SavingUpdateFailed", "Failed to update saving");
                }

                Console.WriteLine($"[UPDATE] Added {transaction.Amount} to saving {transaction.SavingId}");
            }
        }

        return transaction.ToDto();
    }

    */

    public async Task<ErrorOr<Success>> DeleteTransactionAsync(
    int id,
    CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id);
        if (transaction == null)
            return TransactionErrors.NotFound;

        var categoryId = transaction.CategoryId;
        var savingId = transaction.SavingId;
        var amountInUSD = transaction.Amount;
        var type = transaction.Type;
        var userId = transaction.UserId;

        await _transactionRepository.DeleteAsync(transaction);
        await _transactionRepository.SaveChangesAsync();

        //  Handle deletion based on type
        if (type == TransactionType.INCOME || type == TransactionType.EXPENSE)
        {
            //  Revert amount from category (CategoryService handles budgets)
            var categoryResult = await _categoryService.UpdateCategoryAsync(
                categoryId.Value,
                type,
                -amountInUSD,
                cancellationToken);

            if (categoryResult.IsError)
            {
                Console.WriteLine($"[ERROR] Failed to update category: {categoryResult.Errors.First().Description}");
                return Error.Failure("Transaction.CategoryUpdateFailed", "Failed to update category");
            }

            Console.WriteLine($"[DELETE] Removed {type} from category {categoryId}: -{amountInUSD}");
        }
        else if (type == TransactionType.SAVING)
        {
            //  Remove saving transaction through service
            if (savingId.HasValue)
            {
                var removeResult = await _savingService.RemoveSavingTransactionAsync(
                    savingId.Value,
                    userId,
                    amountInUSD,
                    cancellationToken);

                if (removeResult.IsError)
                {
                    Console.WriteLine($"[ERROR] Failed to remove saving: {removeResult.Errors.First().Description}");
                    return Error.Failure("Transaction.SavingUpdateFailed", "Failed to remove saving");
                }

                Console.WriteLine($"[DELETE] Removed {amountInUSD} from saving {savingId}");
            }
        }

        return Result.Success;
    }

    public async Task<ErrorOr<MlTransactionExtractionResult>> ExtractTransactionFromImageAsync(
        Stream imageStream,
        string contentType,
        IEnumerable<string> availableCategories,
        CancellationToken cancellationToken = default)
    {
        if (imageStream == null || !imageStream.CanRead)
            return Error.Validation("Image.Invalid", "Image stream is invalid.");

        if (contentType != "image/jpeg" && contentType != "image/png" && contentType != "image/webp")
            return Error.Validation("Image.UnsupportedType", "Only JPEG, PNG, and WEBP images are supported.");

        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);

        if (memoryStream.Length == 0)
            return Error.Validation("Image.Empty", "Image is empty.");

        if (memoryStream.Length > 10 * 1024 * 1024)
            return Error.Validation("Image.TooLarge", "Image must be smaller than 10MB.");

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return Error.Failure("OpenAI.MissingApiKey", "OPENAI_API_KEY environment variable is missing.");

        var base64Image = Convert.ToBase64String(memoryStream.ToArray());
        var dataUrl = $"data:{contentType};base64,{base64Image}";
        var categories = string.Join(", ", availableCategories);

        var prompt = $$"""
        You are a financial transaction extractor.

        Analyze the image. It may be a receipt, invoice, salary slip, bank screenshot, transfer confirmation, or handwritten transaction note.

        Decide whether it is:
        - expense
        - income
        - unknown

        Extract the final transaction amount, not subtotal or tax.
        Pick the closest category from this list:
        {{categories}}

        Return only valid JSON. No markdown. No explanation.

        JSON schema:
        {
        "type": "expense | income | unknown",
        "amount": number | null,
        "currency": string | null,
        "date": "YYYY-MM-DD" | null,
        "merchantOrSource": string | null,
        "category": string | null,
        "confidence": number,
        "notes": string | null
        }
        """;

        var requestBody = new
        {
            model = "gpt-5.5",
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = prompt
                        },
                        new
                        {
                            type = "input_image",
                            image_url = dataUrl
                        }
                    }
                }
            }
        };

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.PostAsync(
            "https://api.openai.com/v1/responses",
            content,
            cancellationToken);

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Error.Failure("OpenAI.RequestFailed", responseJson);

        var outputText = ExtractOutputText(responseJson);
        if (string.IsNullOrWhiteSpace(outputText))
            return Error.Failure("OpenAI.EmptyResponse", "The model did not return extraction JSON.");

        try
        {
            var result = JsonSerializer.Deserialize<MlTransactionExtractionResult>(
                outputText,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (result == null)
                return Error.Failure("OpenAI.InvalidJson", "Could not parse extraction result.");

            result.Type = result.Type.ToLowerInvariant();

            if (result.Type != "expense" && result.Type != "income")
                result.Type = "unknown";

            return result;
        }
        catch (JsonException)
        {
            return Error.Failure("OpenAI.InvalidJson", outputText);
        }
    }

    private static string? ExtractOutputText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);

        if (!document.RootElement.TryGetProperty("output", out var output))
            return null;

        foreach (var outputItem in output.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var content))
                continue;

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    contentItem.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

}