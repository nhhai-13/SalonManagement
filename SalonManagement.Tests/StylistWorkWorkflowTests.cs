using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class StylistWorkWorkflowTests
{
    [Fact]
    public async Task Stylist_can_only_see_own_appointments_and_start_then_complete_them()
    {
        await using var db = CreateDb();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 14, 4, 0, 0, TimeSpan.Zero));
        await SeedAsync(db);
        var controller = CreateController(db, clock);

        var listResult = Assert.IsType<OkObjectResult>(await controller.Appointments(new DateTime(2026, 10, 14), CancellationToken.None));
        var appointments = Assert.IsType<List<StylistAppointmentResponse>>(listResult.Value);
        var appointment = Assert.Single(appointments);
        Assert.Equal("090****678", appointment.CustomerPhone);
        Assert.Equal(60, appointment.ExpectedDurationMinutes);
        Assert.Equal("Khách cần tư vấn", appointment.CustomerNote);

        Assert.IsType<OkObjectResult>(await controller.Start(appointment.AppointmentId, CancellationToken.None));
        Assert.Equal("InProgress", (await db.Appointments.SingleAsync()).Status);
        clock.Now = clock.Now.AddMinutes(85);
        var completed = Assert.IsType<OkObjectResult>(await controller.Complete(appointment.AppointmentId, CancellationToken.None));
        Assert.Equal("Completed", (await db.Appointments.SingleAsync()).Status);
        var saved = await db.Appointments.SingleAsync();
        Assert.Equal(clock.Now, saved.ServiceCompletedAt);
        Assert.Equal(new[] { "service-start", "service-complete" }, await db.AppointmentAudits.OrderBy(a => a.Id).Select(a => a.Action).ToArrayAsync());
        Assert.NotNull(completed.Value);
    }

    [Fact]
    public async Task Stylist_cannot_start_before_customer_has_arrived()
    {
        await using var db = CreateDb();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 14, 4, 0, 0, TimeSpan.Zero));
        await SeedAsync(db);
        var appointment = await db.Appointments.SingleAsync();
        appointment.Status = "Confirmed";
        await db.SaveChangesAsync();

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db, clock).Start(appointment.AppointmentId, CancellationToken.None));
        var message = result.Value!.GetType().GetProperty("message")!.GetValue(result.Value)!.ToString();
        Assert.Contains("Đã đến", message);
        Assert.Equal("Confirmed", (await db.Appointments.SingleAsync()).Status);
    }

    private static ApplicationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new HttpContextAccessor());

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        var stylist = new Stylist { StylistId = 5, FullName = "Thợ A", Email = "stylist@test.local", IsActive = true };
        db.Stylists.Add(stylist);
        db.Users.Add(new ApplicationUser { Id = "user-stylist", UserName = "stylist@test.local", Email = "stylist@test.local", StylistId = 5 });
        var service = new Service { ServiceId = 7, ServiceName = "Cắt", DurationMinutes = 60, IsActive = true };
        db.Services.Add(service);
        var customer = new Customer { CustomerId = 8, FullName = "Khách", Phone = "09012345678" };
        db.Customers.Add(customer);
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 12,
            StylistId = 5,
            Customer = customer,
            AppointmentDate = new DateTime(2026, 10, 14),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Status = AppointmentStatuses.Arrived,
            Notes = "Khách cần tư vấn",
            AppointmentServices = [new AppointmentService { ServiceId = 7, DurationMinutes = 60, Service = service }]
        });
        await db.SaveChangesAsync();
    }

    private static StylistWorkController CreateController(ApplicationDbContext db, TestClock clock)
    {
        var controller = new StylistWorkController(db, clock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "user-stylist"), new Claim(ClaimTypes.Role, UserRoles.Stylist)], "test"))
                }
            }
        };
        return controller;
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
