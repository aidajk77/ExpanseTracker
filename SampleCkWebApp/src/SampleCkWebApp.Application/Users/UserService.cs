using ErrorOr;
using SampleCkWebApp.Application.Common.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Users.Interfaces.Application;
using Contracts.DTOs.User;
using Domain.Entities;
using api.Mappers;
using Domain.Errors;
using SampleCkWebApp.Application.Users.Interfaces.Infrastructure;
using SampleCkWebApp.Application.Currencies.Interfaces.Infrastructure;

namespace SampleCkWebApp.Application.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrencyRepository _currencyRepository;

    public UserService(IUserRepository userRepository, ICurrencyRepository currencyRepository)
    {
        _userRepository = userRepository;
        _currencyRepository = currencyRepository;
    }

    public async Task<ErrorOr<IEnumerable<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<ErrorOr<UserDto>> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return UserErrors.NotFound;

        return user.ToDto();
    }

    public async Task<ErrorOr<UserDto>> UpdateUserAsync(int id, UpdateUserDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return UserErrors.NotFound;

        // Apply updates only if provided
        if (!string.IsNullOrEmpty(request.Username))
            user.Username = request.Username;

        if (!string.IsNullOrEmpty(request.Email))
        {
            var normalizedEmail = request.Email.ToLower().Trim();
            
            // ✅ OPTIMIZED - Single database query for email check
            var emailExistsForOther = await _userRepository.EmailExistsForOtherUserAsync(normalizedEmail, id);
            if (emailExistsForOther)
                return UserErrors.DuplicateEmail;

            user.Email = normalizedEmail;
        }

        if (request.CurrencyId.HasValue)
        {
            // Validate currency exists
            var currencyExists = await _currencyRepository.GetByIdAsync(request.CurrencyId.Value);
            if (currencyExists == null)
                return UserErrors.InvalidCurrency;

            user.CurrencyId = request.CurrencyId.Value;
        }
        
        if (request.Role.HasValue)
            user.Role = request.Role.Value;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return user.ToDto();
    }

    public async Task<ErrorOr<Success>> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return UserErrors.NotFound;

        await _userRepository.DeleteAsync(user);
        await _userRepository.SaveChangesAsync();

        return Result.Success;
    }
}
