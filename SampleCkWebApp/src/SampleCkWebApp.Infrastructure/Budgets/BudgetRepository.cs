using SampleCkWebApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SampleCkWebApp.Application.Common.Interfaces.Infrastructure;
using Domain.Entities;
using SampleCkWebApp.Application.Budget.Interfaces.Infrastructure;

namespace SampleCkWebApp.Infrastructure.Budgets.Repositories;

public class BudgetRepository : IBudgetRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<Budget> _budgetSet;

    public BudgetRepository(ApplicationDbContext context)
    {
        _context = context;
        _budgetSet = context.Set<Budget>();
    }

    public async Task<IEnumerable<Budget>> GetUserBudgetsAsync(int userId)
    {
        return await _budgetSet
            .AsNoTracking()
            .Where(b => b.Category.UserId == userId)  // ✅ Filter at DB level
            .Include(b => b.Category)
            .OrderBy(b => b.Category.Name)
            .ToListAsync();
    }

    public async Task<Budget?> GetBudgetForCategoryAndMonthAsync(int categoryId, int month, int year)
    {
        return await _budgetSet
            .AsNoTracking()
            .FirstOrDefaultAsync(b =>
                b.CategoryId == categoryId &&
                b.Month == month &&
                b.Year == year);
    }

    public async Task<IEnumerable<Budget>> GetUserBudgetsForMonthAsync(int userId, int month, int year)
    {
        return await _budgetSet
            .AsNoTracking()
            .Where(b => 
                b.Category.UserId == userId &&      // ✅ Filter at DB
                b.Month == month &&
                b.Year == year)
            .Include(b => b.Category)
            .OrderBy(b => b.Category.Name)
            .ToListAsync();
    }

    public async Task<(decimal totalBudgetAmount, decimal totalSpentAmount, int budgetCount)> 
        GetUserBudgetSummaryAsync(int userId)
    {
        var summary = await _budgetSet
            .AsNoTracking()
            .Where(b => b.Category.UserId == userId)
            .GroupBy(x => 1)  // Group all into one
            .Select(g => new
            {
                TotalBudget = g.Sum(b => b.AmountLimit),
                TotalSpent = g.Sum(b => b.CurrentAmount),
                Count = g.Count()
            })
            .FirstOrDefaultAsync();

        if (summary == null)
            return (0, 0, 0);

        return (summary.TotalBudget, summary.TotalSpent, summary.Count);
    }

    public async Task<(decimal totalBudgetAmount, decimal totalSpentAmount, int budgetCount)> 
        GetUserBudgetSummaryForMonthAsync(int userId, int month, int year)
    {
        var summary = await _budgetSet
            .AsNoTracking()
            .Where(b => 
                b.Category.UserId == userId &&
                b.Month == month &&
                b.Year == year)
            .GroupBy(x => 1)
            .Select(g => new
            {
                TotalBudget = g.Sum(b => b.AmountLimit),
                TotalSpent = g.Sum(b => b.CurrentAmount),
                Count = g.Count()
            })
            .FirstOrDefaultAsync();

        if (summary == null)
            return (0, 0, 0);

        return (summary.TotalBudget, summary.TotalSpent, summary.Count);
    }

    public async Task<bool> BudgetExistsForMonthAsync(int categoryId, int month, int year)
    {
        return await _budgetSet
            .AsNoTracking()
            .AnyAsync(b => 
                b.CategoryId == categoryId &&
                b.Month == month &&
                b.Year == year);
    }
    public async Task<IEnumerable<Budget>> GetBudgetsByCategoryIdAsync(int categoryId)
    {
        return await _budgetSet
            .Where(b => b.CategoryId == categoryId)
            .ToListAsync();
    }
    public async Task<Budget?> GetByIdAsync(int id)
    {
        return await _budgetSet.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<Budget>> GetAllAsync()
    {
        return await _budgetSet
            .AsNoTracking()
            .Include(b => b.Category)
            .ToListAsync();
    }

    public async Task AddAsync(Budget entity)
    {
        await _budgetSet.AddAsync(entity);
    }

    public async Task AddRangeAsync(IEnumerable<Budget> entities)
    {
        await _budgetSet.AddRangeAsync(entities);
    }

    public async Task UpdateAsync(Budget entity)
    {
        _budgetSet.Update(entity);
    }

    public async Task DeleteAsync(Budget entity)
    {
        _budgetSet.Remove(entity);
    }

    public async Task DeleteRangeAsync(IEnumerable<Budget> entities)
    {
        _budgetSet.RemoveRange(entities);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

}
