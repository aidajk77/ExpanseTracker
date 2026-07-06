using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.DTOs.User;
using Domain.Entities;

namespace api.Mappers
{
    public static class UserMappers
    {
        public static UserDto ToDto(this User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                CurrencyId = user.CurrencyId
            };
        }

        public static User ToModel(this RegisterUserDto dto, string passwordHash)
        {
            return new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = passwordHash,
                Role = Domain.Enums.Role.User,
                CurrencyId = dto.CurrencyId
            };
        }

        public static User ToModel(this LoginUserDto dto, string passwordHash)
    {
        return new User
        {
            Email = dto.Email,
            PasswordHash = passwordHash
        };
    }
    }
}
