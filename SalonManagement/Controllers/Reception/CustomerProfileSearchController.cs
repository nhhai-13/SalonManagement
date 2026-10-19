using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist + "," + UserRoles.Owner)]
[Route("reception/customer-search")]
public sealed class CustomerProfileSearchController(CustomerProfileSearchService search) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q)
    {
        var response = await search.SearchAsync(q);
        return View(new CustomerProfileSearchPageViewModel
        {
            Query = q?.Trim() ?? string.Empty,
            Results = response.Results,
            Message = response.Error ?? (q is not null && response.Results.Count == 0 ? "Không tìm thấy hồ sơ phù hợp." : null)
        });
    }
}
