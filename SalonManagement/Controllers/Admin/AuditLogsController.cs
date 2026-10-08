using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers
{
    [Authorize(Roles = UserRoles.Admin)]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            DateTime? fromDate,
            DateTime? toDate,
            string? userName,
            int page = 1)
        {
            const int pageSize = 50;
            page = Math.Max(1, page);

            var query = _context.AuditLogs
                .AsNoTracking()
                .OrderByDescending(x => x.Timestamp)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(x =>
                    x.Timestamp >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(x =>
                    x.Timestamp < endDate);
            }

            if (!string.IsNullOrWhiteSpace(userName))
            {
                query = query.Where(x =>
                    x.UserName == userName);
            }

            var totalRecords = await query.CountAsync();

            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.UserName = userName;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages =
                (int)Math.Ceiling(totalRecords / (double)pageSize);

            ViewBag.UserNames = await _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.UserName != null)
                .Select(x => x.UserName!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return View(logs);
        }
    }
}
