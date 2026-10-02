using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalonManagement.Controllers.DevTools
{
    /// <summary>
    /// DEV TOOL — Chỉ dùng để test API /api/availability trong giai đoạn phát triển.
    /// Sẽ xoá khi Team C có trang đặt lịch chính thức (Sprint 2).
    /// URL: /dev/availability
    /// </summary>
    [Route("dev/availability")]
    [AllowAnonymous]
    public class AvailabilityDevController : Controller
    {
        /// <summary>
        /// GET: /dev/availability
        /// Render trang test nhập ngày + serviceIds + stylistId.
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Views/DevTools/Availability.cshtml");
        }
    }
}