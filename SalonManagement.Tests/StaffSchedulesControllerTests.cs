using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class StaffSchedulesControllerTests
{
    [Fact]
    public async Task Index_LoadsActiveReceptionistsAndStylistsWithSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();

        var receptionistRole = new Microsoft.AspNetCore.Identity.IdentityRole(UserRoles.Receptionist) { Id = "reception" };
        var stylistRole = new Microsoft.AspNetCore.Identity.IdentityRole(UserRoles.Stylist) { Id = "stylist" };
        var receptionist = new ApplicationUser { Id = "u1", UserName = "reception@salon.local", Email = "reception@salon.local", FullName = "Lễ tân", IsActive = true };
        var stylist = new ApplicationUser { Id = "u2", UserName = "stylist@salon.local", Email = "stylist@salon.local", FullName = "Thợ", IsActive = true };
        db.AddRange(receptionistRole, stylistRole, receptionist, stylist);
        db.UserRoles.AddRange(
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = receptionist.Id, RoleId = receptionistRole.Id },
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = stylist.Id, RoleId = stylistRole.Id });
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            UserId = receptionist.Id,
            WorkDate = new DateTime(2026, 10, 12),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17),
            Notes = "Ca cả ngày"
        });
        await db.SaveChangesAsync();

        var result = await new StaffSchedulesController(db).Index(new DateTime(2026, 10, 12));

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StaffScheduleWeekViewModel>(view.Model);
        Assert.Equal(2, model.Staff.Count);
        Assert.Single(model.Shifts);
        Assert.Equal(UserRoles.Receptionist, model.Staff[0].Role);
    }
}
