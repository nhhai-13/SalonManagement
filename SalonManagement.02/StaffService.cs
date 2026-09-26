using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SalonManagement.StaffManagement
{
    public class StaffService : IStaffService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public StaffService(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<ServiceResponseModel> CreateStaffAsync(CreateStaffDto dto)
        {
            // Tiêu chí 2: Email trùng với tài khoản đã có bị từ chối kèm thông báo chỉ rõ trường bị trùng
            var existingEmail = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

            if (existingEmail != null)
            {
                return new ServiceResponseModel
                {
                    Success = false,
                    Field = "Email",
                    Message = "Địa chỉ email này đã được sử dụng bởi một tài khoản khác trong hệ thống."
                };
            }

            // Tiêu chí 3: Tạo tài khoản mới sinh mật khẩu tạm ngẫu nhiên
            string tempPassword = GenerateStrongTemporaryPassword();
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(tempPassword);

            var newStaff = new User
            {
                FullName = dto.FullName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                Role = dto.Role,
                IsActive = true,
                PasswordHash = hashedPassword,
                MustChangePasswordOnNextLogin = true, // Buộc đổi mật khẩu ở lần đăng nhập đầu tiên
                TokenVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newStaff);
            await _context.SaveChangesAsync();

            // Gửi email thông báo mật khẩu tạm thời
            string subject = "Thông tin cấp tài khoản hệ thống SalonManagement";
            string body = $"Xin chào {newStaff.FullName},\n\n" +
                          $"Tài khoản nhân sự của bạn đã được khởi tạo thành công.\n" +
                          $"Tên đăng nhập (Email): {newStaff.Email}\n" +
                          $"Mật khẩu tạm thời của bạn là: {tempPassword}\n\n" +
                          $"Lưu ý quan trọng: Hệ thống bắt buộc bạn phải đổi mật khẩu mới ngay trong lần đăng nhập đầu tiên để đảm bảo bảo mật.\n\n" +
                          $"Trân trọng,\nBan quản trị SalonManagement.";

            try
            {
                await _emailService.SendEmailAsync(newStaff.Email, subject, body);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi gửi email nhưng không làm gián đoạn luồng tạo tài khoản trong DB
                Console.WriteLine($"[Warning] Không thể gửi email tới {newStaff.Email}: {ex.Message}");
            }

            return new ServiceResponseModel
            {
                Success = true,
                Message = "Tạo tài khoản nhân sự thành công và đã gửi thông tin mật khẩu tạm qua email.",
                Data = new StaffResponseDto
                {
                    Id = newStaff.Id,
                    FullName = newStaff.FullName,
                    Email = newStaff.Email,
                    PhoneNumber = newStaff.PhoneNumber,
                    Role = newStaff.Role,
                    IsActive = newStaff.IsActive,
                    MustChangePasswordOnNextLogin = newStaff.MustChangePasswordOnNextLogin,
                    CreatedAt = newStaff.CreatedAt
                }
            };
        }

        public async Task<ServiceResponseModel> UpdateStaffAsync(int id, UpdateStaffDto dto)
        {
            var staff = await _context.Users.FindAsync(id);
            if (staff == null)
            {
                return new ServiceResponseModel { Success = false, Message = "Không tìm thấy thông tin nhân sự cần cập nhật." };
            }

            // Nếu thay đổi trạng thái từ Active (true) sang Inactive (false)
            if (staff.IsActive && !dto.IsActive)
            {
                var validationCheck = await ValidateAndPerformDeactivation(staff);
                if (!validationCheck.Success) return validationCheck;
            }

            staff.FullName = dto.FullName.Trim();
            staff.PhoneNumber = dto.PhoneNumber.Trim();
            staff.Role = dto.Role;
            staff.IsActive = dto.IsActive;
            staff.UpdatedAt = DateTime.UtcNow;

            // Tiêu chí 4: Nếu ngưng hoạt động, tăng token version để vô hiệu hóa phiên
            if (!dto.IsActive)
            {
                staff.TokenVersion += 1;
            }

            await _context.SaveChangesAsync();

            return new ServiceResponseModel
            {
                Success = true,
                Message = "Cập nhật thông tin nhân sự thành công."
            };
        }

        public async Task<ServiceResponseModel> ChangeStatusAsync(int id, bool newStatus)
        {
            var staff = await _context.Users.FindAsync(id);
            if (staff == null)
            {
                return new ServiceResponseModel { Success = false, Message = "Không tìm thấy tài khoản nhân sự." };
            }

            if (staff.IsActive == newStatus)
            {
                return new ServiceResponseModel { Success = true, Message = "Trạng thái tài khoản không có sự thay đổi." };
            }

            // Nếu thực hiện ngưng hoạt động tài khoản
            if (!newStatus)
            {
                var validationCheck = await ValidateAndPerformDeactivation(staff);
                if (!validationCheck.Success) return validationCheck;
            }
            else
            {
                staff.IsActive = true;
            }

            staff.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            string statusMessage = newStatus 
                ? "Đã kích hoạt lại tài khoản thành công." 
                : "Đã ngưng hoạt động tài khoản. Mọi phiên đăng nhập hiện tại sẽ mất hiệu lực trong vòng 1 phút.";

            return new ServiceResponseModel
            {
                Success = true,
                Message = statusMessage
            };
        }

        public async Task<ServiceResponseModel> GetStaffListAsync(StaffFilterQueryDto queryDto)
        {
            var query = _context.Users.AsQueryable();

            // Tìm kiếm theo từ khóa (Tên hoặc Email)
            if (!string.IsNullOrWhiteSpace(queryDto.SearchKeyword))
            {
                string keyword = queryDto.SearchKeyword.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(keyword) || u.Email.ToLower().Contains(keyword));
            }

            // Lọc theo Role
            if (!string.IsNullOrWhiteSpace(queryDto.Role))
            {
                query = query.Where(u => u.Role.ToLower() == queryDto.Role.ToLower());
            }

            // Lọc theo trạng thái Active
            if (queryDto.IsActive.HasValue)
            {
                query = query.Where(u => u.IsActive == queryDto.IsActive.Value);
            }

            int totalRecords = await query.CountAsync();

            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((queryDto.PageNumber - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .Select(u => new StaffResponseDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    MustChangePasswordOnNextLogin = u.MustChangePasswordOnNextLogin,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return new ServiceResponseModel
            {
                Success = true,
                Message = "Lấy danh sách nhân sự thành công.",
                Data = new
                {
                    TotalRecords = totalRecords,
                    PageNumber = queryDto.PageNumber,
                    PageSize = queryDto.PageSize,
                    Items = items
                }
            };
        }

        public async Task<ServiceResponseModel> GetStaffByIdAsync(int id)
        {
            var staff = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new StaffResponseDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    MustChangePasswordOnNextLogin = u.MustChangePasswordOnNextLogin,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (staff == null)
            {
                return new ServiceResponseModel { Success = false, Message = "Không tìm thấy tài khoản nhân sự." };
            }

            return new ServiceResponseModel { Success = true, Data = staff };
        }

        /// <summary>
        /// Hàm nội bộ kiểm tra ràng buộc bảo mật trước khi ngưng hoạt động tài khoản
        /// </summary>
        private async Task<ServiceResponseModel> ValidateAndPerformDeactivation(User staff)
        {
            // Tiêu chí 5: Không cho phép ngưng hoạt động tài khoản quản trị cuối cùng còn lại trong hệ thống
            if (staff.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                int activeAdminCount = await _context.Users
                    .CountAsync(u => u.Role.ToLower() == "admin" && u.IsActive == true && u.Id != staff.Id);

                if (activeAdminCount <= 0)
                {
                    return new ServiceResponseModel
                    {
                        Success = false,
                        Message = "Thao tác bị từ chối: Không thể ngưng hoạt động tài khoản quản trị (Admin) cuối cùng còn lại trong hệ thống!"
                    };
                }
            }

            staff.IsActive = false;
            
            // Tiêu chí 4: Tăng TokenVersion để vô hiệu hóa toàn bộ session/token hiện tại trong vòng 1 phút
            staff.TokenVersion += 1;

            return new ServiceResponseModel { Success = true };
        }

        private string GenerateStrongTemporaryPassword()
        {
            string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            string lowercase = "abcdefghijkmnpqrstuvwxyz";
            string digits = "23456789";
            string specials = "@#$!";

            var rnd = new Random();
            string pass = "" + uppercase[rnd.Next(uppercase.Length)] +
                          lowercase[rnd.Next(lowercase.Length)] +
                          digits[rnd.Next(digits.Length)] +
                          specials[rnd.Next(specials.Length)];

            string allChars = uppercase + lowercase + digits + specials;
            for (int i = 0; i < 6; i++)
            {
                pass += allChars[rnd.Next(allChars.Length)];
            }
            return pass;
        }
    }
}