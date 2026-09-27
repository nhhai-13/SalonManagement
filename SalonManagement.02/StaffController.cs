using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace SalonManagement.StaffManagement
{
    [Route("api/admin/staff")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StaffController : ControllerBase // ĐÃ SỬA: tên class phải khớp tên constructor
    {
        private readonly IStaffService _staffService;

        public StaffController(IStaffService staffService)
        {
            _staffService = staffService;
        }

        /// <summary>
        /// API Lấy danh sách nhân sự (Có hỗ trợ tìm kiếm, lọc và phân trang)
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetStaffList([FromQuery] StaffFilterQueryDto queryDto)
        {
            var result = await _staffService.GetStaffListAsync(queryDto);
            return Ok(result);
        }

        /// <summary>
        /// API Xem chi tiết một nhân sự theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStaffById(int id)
        {
            var result = await _staffService.GetStaffByIdAsync(id);
            if (!result.Success) return NotFound(new { message = result.Message });

            return Ok(result);
        }

        /// <summary>
        /// API Tạo mới tài khoản nhân sự (Đáp ứng toàn bộ AC về biểu mẫu, trùng email, mật khẩu tạm)
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _staffService.CreateStaffAsync(dto, GetCurrentUserId());
            if (!result.Success)
            {
                return BadRequest(new { field = result.Field, message = result.Message });
            }

            return Ok(result);
        }

        /// <summary>
        /// API Cập nhật thông tin nhân sự
        /// </summary>
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateStaff(int id, [FromBody] UpdateStaffDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _staffService.UpdateStaffAsync(id, dto, GetCurrentUserId());
            if (!result.Success) return BadRequest(new { message = result.Message });

            return Ok(result);
        }

        /// <summary>
        /// API Đổi trạng thái Active/Inactive (Xử lý hủy phiên và chặn khóa Admin cuối)
        /// </summary>
        [HttpPatch("status/{id}")]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusDto body)
        {
            // ĐÃ SỬA: nhận vào ChangeStatusDto thay vì bool trần —
            // ASP.NET Core bind primitive (bool) trực tiếp từ JSON body dễ lỗi/không rõ ràng.
            var result = await _staffService.ChangeStatusAsync(id, body.IsActive, GetCurrentUserId());
            if (!result.Success) return BadRequest(new { message = result.Message });

            return Ok(result);
        }

        /// <summary>
        /// API MỚI: Thu hồi toàn bộ phiên đăng nhập của một tài khoản nhân sự (Bước 5)
        /// </summary>
        [HttpPost("{id}/revoke-session")]
        public async Task<IActionResult> RevokeSession(int id)
        {
            var result = await _staffService.RevokeSessionAsync(id, GetCurrentUserId());
            if (!result.Success) return BadRequest(new { message = result.Message });

            return Ok(result);
        }

        /// <summary>
        /// Lấy Id của Admin đang đăng nhập từ claim JWT, dùng để ghi audit log.
        /// Đổi tên claim "sub" / ClaimTypes.NameIdentifier cho khớp với cách bạn generate JWT lúc login.
        /// </summary>
        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

            if (idClaim == null || !int.TryParse(idClaim, out var id))
                throw new UnauthorizedAccessException("Không xác định được người dùng hiện tại từ token.");

            return id;
        }
    }

    // DTO nhỏ, tạo thêm vào StaffDtosAndModels.cs
    public class ChangeStatusDto
    {
        public bool IsActive { get; set; }
    }
}