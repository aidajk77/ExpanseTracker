using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Contracts.DTOs.User
{
    public class RegisterUserDto
    {
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public int CurrencyId { get; set; }  
    }
}
