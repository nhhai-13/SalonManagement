using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace SalonManagement.StaffManagement
{
    [Route("api/staff")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StaffController : ControllerBase
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

            var result = await _staffService.CreateStaffAsync(dto);
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

            var result = await _staffService.UpdateStaffAsync(id, dto);
            if (!result.Success) return BadRequest(new { message = result.Message });

            return Ok(result);
        }

        /// <summary>
        /// API Đổi trạng thái Active/Inactive (Xử lý hủy phiên 1 phút và chặn khóa Admin cuối)
        /// </summary>
        [HttpPatch("status/{id}")]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] bool isActive)
        {
            var result = await _staffService.ChangeStatusAsync(id, isActive);
            if (!result.Success) return BadRequest(new { message = result.Message });

            return Ok(result);
        }
    }
}