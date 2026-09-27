using System;
using System.Linq;
using System.Text.Json;
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

        public async Task<ServiceResponseModel> CreateStaffAsync(CreateStaffDto dto, int actorId)
        {
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
                MustChangePasswordOnNextLogin = true,
                TokenVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newStaff);
            await _context.SaveChangesAsync(); // Save trước để newStaff.Id có giá trị thật

            _context.Add(new StaffAuditLog
            {
                Action = "CREATE",
                ActorId = actorId,
                TargetId = newStaff.Id,
                NewValuesJson = JsonSerializer.Serialize(new { newStaff.FullName, newStaff.Email, newStaff.Role }),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

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

        public async Task<ServiceResponseModel> UpdateStaffAsync(int id, UpdateStaffDto dto, int actorId)
        {
            var staff = await _context.Users.FindAsync(id);
            if (staff == null)
            {
                return new ServiceResponseModel { Success = false, Message = "Không tìm thấy thông tin nhân sự cần cập nhật." };
            }

            // ĐÃ BỎ toàn bộ logic deactivate ở đây — UpdateStaffDto không còn IsActive nữa.
            // Việc bật/tắt trạng thái tài khoản giờ CHỈ đi qua ChangeStatusAsync bên dưới.

            var oldValues = JsonSerializer.Serialize(new { staff.FullName, staff.PhoneNumber, staff.Role });

            staff.FullName = dto.FullName.Trim();
            staff.PhoneNumber = dto.PhoneNumber.Trim();
            staff.Role = dto.Role;
            staff.UpdatedAt = DateTime.UtcNow;
            staff.UpdatedBy = actorId;

            var newValues = JsonSerializer.Serialize(new { staff.FullName, staff.PhoneNumber, staff.Role });

            _context.Add(new StaffAuditLog
            {
                Action = "UPDATE",
                ActorId = actorId,
                TargetId = staff.Id,
                OldValuesJson = oldValues,
                NewValuesJson = newValues
            });

            await _context.SaveChangesAsync();

            return new ServiceResponseModel
            {
                Success = true,
                Message = "Cập nhật thông tin nhân sự thành công."
            };
        }

        public async Task<ServiceResponseModel> ChangeStatusAsync(int id, bool newStatus, int actorId)
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

            if (!newStatus)
            {
                // ---- RULE BẢO VỆ ADMIN CUỐI CÙNG nằm bên trong hàm này ----
                var validationCheck = await ValidateAndPerformDeactivation(staff, actorId);
                if (!validationCheck.Success) return validationCheck;
            }
            else
            {
                staff.IsActive = true;
                staff.DeactivatedAt = null;
                staff.DeactivatedBy = null;
            }

            staff.UpdatedAt = DateTime.UtcNow;
            staff.UpdatedBy = actorId;

            _context.Add(new StaffAuditLog
            {
                Action = newStatus ? "ACTIVATE" : "DEACTIVATE",
                ActorId = actorId,
                TargetId = staff.Id
            });

            await _context.SaveChangesAsync();

            string statusMessage = newStatus
                ? "Đã kích hoạt lại tài khoản thành công."
                : "Đã ngưng hoạt động tài khoản. Mọi phiên đăng nhập hiện tại đã mất hiệu lực ngay lập tức.";

            return new ServiceResponseModel
            {
                Success = true,
                Message = statusMessage
            };
        }

        public async Task<ServiceResponseModel> GetStaffListAsync(StaffFilterQueryDto queryDto)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(queryDto.SearchKeyword))
            {
                string keyword = queryDto.SearchKeyword.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(keyword) || u.Email.ToLower().Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.Role))
            {
                query = query.Where(u => u.Role.ToLower() == queryDto.Role.ToLower());
            }

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

        // ===== METHOD MỚI: BƯỚC 5 - REVOKE SESSION =====
        public async Task<ServiceResponseModel> RevokeSessionAsync(int id, int actorId)
        {
            var staff = await _context.Users.FindAsync(id);
            if (staff == null)
            {
                return new ServiceResponseModel { Success = false, Message = "Không tìm thấy tài khoản nhân sự." };
            }

            // Tăng TokenVersion -> mọi JWT cũ (mang version thấp hơn) bị từ chối ngay ở request tiếp theo
            staff.TokenVersion += 1;
            staff.UpdatedAt = DateTime.UtcNow;

            _context.Add(new StaffAuditLog
            {
                Action = "REVOKE_SESSION",
                ActorId = actorId,
                TargetId = staff.Id
            });

            await _context.SaveChangesAsync();

            return new ServiceResponseModel
            {
                Success = true,
                Message = "Đã thu hồi toàn bộ phiên đăng nhập của tài khoản."
            };
        }

        // ===== METHOD MỚI: BƯỚC 4 - ĐỔI MẬT KHẨU LẦN ĐẦU =====
        public async Task<ServiceResponseModel> ChangePasswordFirstTimeAsync(int userId, ChangePasswordFirstTimeDto dto)
        {
            var staff = await _context.Users.FindAsync(userId);
            if (staff == null)
            {
                return new ServiceResponseModel { Success = false, Message = "Không tìm thấy tài khoản." };
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.OldTempPassword, staff.PasswordHash))
            {
                return new ServiceResponseModel
                {
                    Success = false,
                    Field = "OldTempPassword",
                    Message = "Mật khẩu tạm thời không chính xác."
                };
            }

            staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            staff.MustChangePasswordOnNextLogin = false;
            staff.PasswordChangedAt = DateTime.UtcNow;
            staff.UpdatedAt = DateTime.UtcNow;

            // Thu hồi token cũ (token đang mang claim mustChangePassword=true) -> bắt đăng nhập lại
            staff.TokenVersion += 1;

            _context.Add(new StaffAuditLog
            {
                Action = "CHANGE_PASSWORD",
                ActorId = userId,
                TargetId = userId
            });

            await _context.SaveChangesAsync();

            return new ServiceResponseModel
            {
                Success = true,
                Message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."
            };
        }

        /// <summary>
        /// Hàm nội bộ kiểm tra ràng buộc bảo mật trước khi ngưng hoạt động tài khoản
        /// </summary>
        private async Task<ServiceResponseModel> ValidateAndPerformDeactivation(User staff, int actorId)
        {
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
            staff.DeactivatedAt = DateTime.UtcNow;
            staff.DeactivatedBy = actorId;

            // Tăng TokenVersion để vô hiệu hóa toàn bộ session/token hiện tại ngay lập tức
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