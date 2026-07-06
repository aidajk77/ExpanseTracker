using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Domain.Enums;

namespace Contracts.DTOs.User
{
    public class UpdateUserDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public int? CurrencyId { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Role? Role { get; set; }
    }
}
