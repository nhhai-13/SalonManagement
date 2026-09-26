using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace SalonManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _dbContext;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var model = new HomeViewModel
            {
                Services = await _dbContext.Services.AsNoTracking().Where(service => service.IsActive).OrderBy(service => service.ServiceName).Take(6).ToListAsync(),
                Stylists = await _dbContext.Stylists.AsNoTracking().Where(stylist => stylist.IsActive).OrderBy(stylist => stylist.FullName).Take(3).ToListAsync()
            };
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
