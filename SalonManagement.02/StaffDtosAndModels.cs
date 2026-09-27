using System;
using System.ComponentModel.DataAnnotations;

namespace SalonManagement.StaffManagement
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string PhoneNumber { get; set; }

        [Required]
        public string Role { get; set; } // "Admin", "Staff", "Manager"

        public bool IsActive { get; set; } = true;

        [Required]
        public string PasswordHash { get; set; }

        public bool MustChangePasswordOnNextLogin { get; set; } = false;

        public int TokenVersion { get; set; } = 1; // Dùng để quản lý vòng đời session/token

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ===== CÁC FIELD MỚI THÊM =====
        public int? UpdatedBy { get; set; }            // ai vừa sửa thông tin (Id của admin)
        public DateTime? PasswordChangedAt { get; set; } // lần đổi mật khẩu gần nhất
        public DateTime? DeactivatedAt { get; set; }     // thời điểm bị ngưng hoạt động
        public int? DeactivatedBy { get; set; }          // ai deactivate
    }

    public class CreateStaffDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên nhân sự.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 100 ký tự.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng chuẩn.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên lạc.")]
        [RegularExpression(@"^0[0-9]{9,10}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng số 0 và có từ 10-11 chữ số.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò cho nhân sự.")]
        public string Role { get; set; }

        // Không có field Password ở đây — mật khẩu tạm do hệ thống tự sinh (Bước 4)
    }

    public class UpdateStaffDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        public string Role { get; set; }

        // ĐÃ BỎ "IsActive" khỏi đây — chuyển sang 2 endpoint riêng
        // Deactivate/Activate để rule bảo vệ Admin cuối cùng áp dụng được
        // (xem DeactivateStaffDto không cần, dùng route riêng /staff/{id}/deactivate)
    }

    public class StaffFilterQueryDto
    {
        public string SearchKeyword { get; set; } // Tìm kiếm theo tên hoặc email
        public string Role { get; set; }          // Lọc theo vai trò
        public bool? IsActive { get; set; }       // Lọc theo trạng thái
        public int PageNumber { get; set; } = 1;  // Trang hiện tại
        public int PageSize { get; set; } = 10;   // Số lượng bản ghi mỗi trang
    }

    public class StaffResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public bool MustChangePasswordOnNextLogin { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ServiceResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Field { get; set; } // Dùng cho lỗi trùng email chỉ rõ trường bị lỗi
        public object Data { get; set; }
    }

    // ===== MODEL MỚI: AUDIT LOG =====
    public class StaffAuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Action { get; set; } // "CREATE", "UPDATE", "DEACTIVATE", "ACTIVATE", "REVOKE_SESSION", "CHANGE_PASSWORD"

        [Required]
        public int ActorId { get; set; }   // Id admin thực hiện hành động

        [Required]
        public int TargetId { get; set; }  // Id tài khoản bị tác động

        public string OldValuesJson { get; set; }
        public string NewValuesJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ===== DTO MỚI: đổi mật khẩu lần đầu =====
    public class ChangePasswordFirstTimeDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu tạm.")]
        public string OldTempPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
        public string NewPassword { get; set; }
    }
}