using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.DTOs.Budget;
using Domain.Entities;

namespace api.Mappers
{
    public static class BudgetMappers
    {
        public static BudgetDto ToDto(this Budget budget)
        {
            var dto = new BudgetDto
            {
                Id = budget.Id,
                CategoryId = budget.CategoryId,
                UserId = budget.Category?.UserId ?? 0,
                AmountLimit = budget.AmountLimit,
                CurrentAmount = budget.CurrentAmount,
                Month = budget.Month,
                Year = budget.Year,
                CreatedAt = budget.CreatedAt
            };

            return dto;
        }

        public static Budget ToModel(this CreateBudgetDto dto)
        {
            return new Budget
            {
                CategoryId = dto.CategoryId,
                AmountLimit = dto.AmountLimit,
                Month = dto.Month,
                Year = dto.Year
            };
        }
    }
}
