using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;

namespace SalonManagement.Data;

public static class SeedData
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        // =========================
        // 1. SERVICE GROUPS
        // =========================

        var groupData = new[]
        {
            new { Name = "Cắt tóc", Order = 1 },
            new { Name = "Gội và chăm sóc", Order = 2 },
            new { Name = "Uốn - Nhuộm", Order = 3 }
        };

        foreach (var item in groupData)
        {
            if (!await context.ServiceGroups
                    .AnyAsync(g => g.GroupName == item.Name))
            {
                context.ServiceGroups.Add(new ServiceGroup
                {
                    GroupName = item.Name,
                    DisplayOrder = item.Order
                });
            }
        }

        await context.SaveChangesAsync();

        // =========================
        // 2. 5 STYLISTS
        // =========================

        var stylists = new[]
        {
            new Stylist
            {
                FullName = "Nguyễn Minh Anh",
                Phone = "0901000001",
                Email = "minhanh@salon.local",
                Specialty = "Cắt tóc nam",
                ExperienceYears = 5,
                IsActive = true
            },

            new Stylist
            {
                FullName = "Trần Thu Hà",
                Phone = "0901000002",
                Email = "thuha@salon.local",
                Specialty = "Tạo kiểu nữ",
                ExperienceYears = 4,
                IsActive = true
            },

            new Stylist
            {
                FullName = "Lê Hoàng Nam",
                Phone = "0901000003",
                Email = "hoangnam@salon.local",
                Specialty = "Uốn tóc",
                ExperienceYears = 6,
                IsActive = true
            },

            new Stylist
            {
                FullName = "Phạm Ngọc Mai",
                Phone = "0901000004",
                Email = "ngocmai@salon.local",
                Specialty = "Nhuộm tóc",
                ExperienceYears = 3,
                IsActive = true
            },

            new Stylist
            {
                FullName = "Đỗ Quốc Huy",
                Phone = "0901000005",
                Email = "quochuy@salon.local",
                Specialty = "Chăm sóc tóc",
                ExperienceYears = 7,
                IsActive = true
            }
        };

        foreach (var stylist in stylists)
        {
            // Chống tạo stylist trùng bằng Email.
            if (!await context.Stylists
                    .AnyAsync(s => s.Email == stylist.Email))
            {
                context.Stylists.Add(stylist);
            }
        }

        await context.SaveChangesAsync();

        // =========================
        // 3. 12 SERVICES
        // =========================

        var cutGroup = await context.ServiceGroups
            .SingleAsync(g => g.GroupName == "Cắt tóc");

        var careGroup = await context.ServiceGroups
            .SingleAsync(g => g.GroupName == "Gội và chăm sóc");

        var chemicalGroup = await context.ServiceGroups
            .SingleAsync(g => g.GroupName == "Uốn - Nhuộm");

        var services = new[]
        {
            new Service
            {
                ServiceName = "Cắt tóc nam",
                Description = "Cắt và tạo kiểu tóc nam",
                Price = 100000,
                DurationMinutes = 30,
                ServiceGroupId = cutGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Cắt tóc nữ",
                Description = "Cắt và tạo kiểu tóc nữ",
                Price = 150000,
                DurationMinutes = 45,
                ServiceGroupId = cutGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Cắt tạo kiểu",
                Description = "Cắt tóc kết hợp tạo kiểu",
                Price = 200000,
                DurationMinutes = 60,
                ServiceGroupId = cutGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Cắt và gội",
                Description = "Cắt tóc kết hợp gội đầu",
                Price = 180000,
                DurationMinutes = 60,
                ServiceGroupId = cutGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Gội đầu",
                Description = "Gội và làm sạch tóc",
                Price = 80000,
                DurationMinutes = 30,
                ServiceGroupId = careGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Gội dưỡng tóc",
                Description = "Gội kết hợp dưỡng tóc",
                Price = 120000,
                DurationMinutes = 45,
                ServiceGroupId = careGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Ủ phục hồi tóc",
                Description = "Ủ và phục hồi tóc hư tổn",
                Price = 250000,
                DurationMinutes = 60,
                ServiceGroupId = careGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Chăm sóc da đầu",
                Description = "Làm sạch và chăm sóc da đầu",
                Price = 200000,
                DurationMinutes = 45,
                ServiceGroupId = careGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Uốn tóc ngắn",
                Description = "Uốn tạo kiểu cho tóc ngắn",
                Price = 500000,
                DurationMinutes = 90,
                ServiceGroupId = chemicalGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Uốn tóc dài",
                Description = "Uốn tạo kiểu cho tóc dài",
                Price = 700000,
                DurationMinutes = 120,
                ServiceGroupId = chemicalGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Nhuộm tóc cơ bản",
                Description = "Nhuộm tóc với màu cơ bản",
                Price = 600000,
                DurationMinutes = 90,
                ServiceGroupId = chemicalGroup.ServiceGroupId,
                IsActive = true
            },

            new Service
            {
                ServiceName = "Nhuộm tóc thời trang",
                Description = "Nhuộm màu thời trang",
                Price = 850000,
                DurationMinutes = 120,
                ServiceGroupId = chemicalGroup.ServiceGroupId,
                IsActive = true
            }
        };

        foreach (var service in services)
        {
            // Chống tạo dịch vụ trùng bằng ServiceName.
            if (!await context.Services
                    .AnyAsync(s => s.ServiceName == service.ServiceName))
            {
                context.Services.Add(service);
            }
        }

        await context.SaveChangesAsync();

        // =========================
        // 4. SAMPLE USERS
        // =========================

        var sampleUsers = new[]
        {
            new
            {
                Email = "admin@salon.local",
                Password = "Admin123!",
                Role = "Admin"
            },

            new
            {
                Email = "owner@salon.local",
                Password = "Owner123!",
                Role = "Owner"
            },

            new
            {
                Email = "receptionist@salon.local",
                Password = "Reception123!",
                Role = "Receptionist"
            },

            new
            {
                Email = "stylist@salon.local",
                Password = "Stylist123!",
                Role = "Stylist"
            }
        };

        foreach (var item in sampleUsers)
        {
            // Kiểm tra tài khoản đã tồn tại.
            var user = await userManager.FindByEmailAsync(item.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = item.Email,
                    Email = item.Email,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var createResult = await userManager.CreateAsync(
                    user,
                    item.Password);

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        createResult.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        $"Không thể tạo tài khoản {item.Email}: {errors}");
                }
            }

            // Kiểm tra role trước khi gán để chạy seed nhiều lần
            // không tạo dữ liệu role trùng.
            if (!await userManager.IsInRoleAsync(user, item.Role))
            {
                var roleResult = await userManager.AddToRoleAsync(
                    user,
                    item.Role);

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        roleResult.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        $"Không thể gán role {item.Role} " +
                        $"cho {item.Email}: {errors}");
                }
            }
        }
    }
}