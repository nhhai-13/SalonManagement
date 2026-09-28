using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class ServiceStaffCoverageTests
{
    [Fact]
    public async Task IndexCountsOnlyActiveStylistsAndIncludesServicesWithNoActiveStaff()
    {
        await using var db = CreateDb();
        var group = new ServiceGroup { GroupName = "Hair", DisplayOrder = 1 };
        var uncovered = new Service { ServiceName = "No staff", Price = 100, DurationMinutes = 30, ServiceGroup = group };
        var inactiveOnly = new Service { ServiceName = "Inactive only", Price = 100, DurationMinutes = 30, ServiceGroup = group };
        var covered = new Service { ServiceName = "Covered", Price = 100, DurationMinutes = 30, ServiceGroup = group };
        var working = new Stylist { FullName = "Working", Phone = "0900000001", IsActive = true };
        var resting = new Stylist { FullName = "Resting", Phone = "0900000002", IsActive = false };
        db.StylistServices.AddRange(
            new StylistService { Service = inactiveOnly, Stylist = resting },
            new StylistService { Service = covered, Stylist = working },
            new StylistService { Service = covered, Stylist = resting });
        await db.SaveChangesAsync();
        var controller = new ServicesController(db);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        var counts = Assert.IsAssignableFrom<IReadOnlyDictionary<int, int>>(controller.ViewData["ActiveStaffCounts"]);
        Assert.Equal(0, counts.GetValueOrDefault(uncovered.ServiceId));
        Assert.Equal(0, counts.GetValueOrDefault(inactiveOnly.ServiceId));
        Assert.Equal(1, counts.GetValueOrDefault(covered.ServiceId));
    }

    [Fact]
    public async Task IndexCountsMultipleWorkingStylistsForSameService()
    {
        await using var db = CreateDb();
        var service = new Service
        {
            ServiceName = "Color",
            Price = 100,
            DurationMinutes = 30,
            ServiceGroup = new ServiceGroup { GroupName = "Hair", DisplayOrder = 1 }
        };
        db.StylistServices.AddRange(
            new StylistService { Service = service, Stylist = new Stylist { FullName = "A", Phone = "0900000001", IsActive = true } },
            new StylistService { Service = service, Stylist = new Stylist { FullName = "B", Phone = "0900000002", IsActive = true } },
            new StylistService { Service = service, Stylist = new Stylist { FullName = "C", Phone = "0900000003", IsActive = true } });
        await db.SaveChangesAsync();
        var controller = new ServicesController(db);

        await controller.Index();

        var counts = Assert.IsAssignableFrom<IReadOnlyDictionary<int, int>>(controller.ViewData["ActiveStaffCounts"]);
        Assert.Equal(3, counts[service.ServiceId]);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
