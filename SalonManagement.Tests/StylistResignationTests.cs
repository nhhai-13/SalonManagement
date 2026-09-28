using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class StylistResignationTests
{
    [Fact]
    public async Task Resign_DeactivatesStylistAndPreservesServicesAndAppointmentHistory()
    {
        await using var db = CreateDb();
        var stylist = new Stylist { FullName = "Nguyễn Văn A", Phone = "0900000001", IsActive = true };
        var service = new Service { ServiceName = "Cut", Price = 100, DurationMinutes = 30 };
        var customer = new Customer { FullName = "Customer", Phone = "0900000010" };
        db.Stylists.Add(stylist);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.StylistServices.Add(new StylistService { Stylist = stylist, Service = service });
        db.Appointments.AddRange(
            new Appointment
            {
                Stylist = stylist, Customer = customer, AppointmentDate = DateTime.Today.AddDays(-1),
                StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(10.5), Status = "Completed"
            },
            new Appointment
            {
                Stylist = stylist, Customer = customer, AppointmentDate = DateTime.Today.AddDays(2),
                StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(11.5), Status = "Pending"
            });
        await db.SaveChangesAsync();
        var controller = CreateManagementController(db);

        var result = await controller.Resign(stylist.StylistId);

        Assert.IsType<RedirectToActionResult>(result);
        var savedStylist = await db.Stylists.SingleAsync(item => item.StylistId == stylist.StylistId);
        Assert.False(savedStylist.IsActive);
        Assert.NotNull(savedStylist.UpdatedAt);
        Assert.Equal(1, await db.StylistServices.CountAsync(item => item.StylistId == stylist.StylistId));
        var appointments = await db.Appointments.Include(item => item.Stylist)
            .OrderBy(item => item.AppointmentDate).ToListAsync();
        Assert.Equal(2, appointments.Count);
        Assert.Equal("Completed", appointments[0].Status);
        Assert.Equal("Pending", appointments[1].Status);
        Assert.All(appointments, item =>
        {
            Assert.Equal(stylist.StylistId, item.StylistId);
            Assert.Equal("Nguyễn Văn A", item.Stylist.FullName);
        });
    }

    [Fact]
    public async Task Resign_ReturnsNotFoundForUnknownStylist_AndIsManagementOnly()
    {
        await using var db = CreateDb();
        var controller = CreateManagementController(db);

        Assert.IsType<NotFoundResult>(await controller.Resign(123));
        var authorize = typeof(StylistManagementController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();
        Assert.Equal(RoleGroups.Management, authorize.Roles);
    }

    [Fact]
    public async Task BookingFiltersResignedStylistsAndRejectsDirectAppointmentRequest()
    {
        await using var db = CreateDb();
        var service = new Service { ServiceName = "Color", Price = 100, DurationMinutes = 30, IsActive = true };
        var working = new Stylist { FullName = "Working stylist", Phone = "0900000001", IsActive = true };
        var resigned = new Stylist { FullName = "Resigned stylist", Phone = "0900000002", IsActive = false };
        db.StylistServices.AddRange(
            new StylistService { Service = service, Stylist = working },
            new StylistService { Service = service, Stylist = resigned });
        await db.SaveChangesAsync();
        var controller = new BookingController(db);

        var listResult = Assert.IsType<OkObjectResult>(await controller.GetStylists(service.ServiceId));
        var json = System.Text.Json.JsonSerializer.Serialize(listResult.Value);
        Assert.Contains("Working stylist", json);
        Assert.DoesNotContain("Resigned stylist", json);

        var createResult = await controller.Create(new CreateBookingRequest
        {
            CustomerName = "Customer",
            Phone = "0900000010",
            ServiceId = service.ServiceId,
            StylistId = resigned.StylistId,
            AppointmentDate = DateTime.Today.AddDays(1),
            StartTime = TimeSpan.FromHours(10)
        });

        Assert.IsType<BadRequestObjectResult>(createResult);
        Assert.Empty(await db.Appointments.ToListAsync());
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private static StylistManagementController CreateManagementController(ApplicationDbContext db)
    {
        var root = Path.GetTempPath();
        var environment = new TestEnvironment(root);
        var controller = new StylistManagementController(db, environment, NullLogger<StylistManagementController>.Instance);
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new EmptyTempDataProvider());
        return controller;
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SalonManagement.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
