using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public class PublicServiceSearchTests
{
    [Theory]
    [InlineData("Cắt tóc", "Cắt tóc")]
    [InlineData("tóc", "Cắt tóc")]
    [InlineData("CẮT TÓC", "Cắt tóc")]
    [InlineData("cắt tóc", "Cắt tóc")]
    [InlineData("cat toc", "Cắt tóc")]
    [InlineData("  CAT TOC  ", "Cắt tóc")]
    [InlineData("Ca\u0306\u0301t to\u0301c", "Cắt tóc")]
    [InlineData("goi", "Gội đầu", "Gội thư giãn")]
    [InlineData("GOI DAU", "Gội đầu")]
    [InlineData("không tồn tại")]
    [InlineData("<script>")]
    public async Task Search_MatchesVietnameseNamesAndPreservesGroups(string keyword, params string[] expected)
    {
        await using var db = await CreateDb();
        var controller = Controller(db);
        foreach (var result in new[] { await controller.Index(keyword), await controller.Public(keyword) })
        {
            var model = Assert.IsType<PublicServiceCatalog>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(keyword.Trim(), model.Search);
            Assert.Equal(expected, model.Groups.SelectMany(g => g.Services).Select(s => s.ServiceName));
            Assert.All(model.Groups, group => Assert.NotEmpty(group.Services));
            if (expected.Length == 0) Assert.Empty(model.Groups);
            if (keyword == "goi") Assert.Equal(new[] { "Gội", "Ungrouped" }, model.Groups.Select(g => g.Name));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ClearSearch_RestoresAllEligibleServices(string? cleared)
    {
        await using var db = await CreateDb();
        var controller = Controller(db);
        var filtered = Assert.IsType<PublicServiceCatalog>(Assert.IsType<ViewResult>(await controller.Public("cat toc")).Model);
        Assert.Single(filtered.Groups.SelectMany(g => g.Services));
        var model = Assert.IsType<PublicServiceCatalog>(Assert.IsType<ViewResult>(await controller.Public(cleared)).Model);
        Assert.Equal(string.Empty, model.Search);
        Assert.Equal(new[] { "Gội đầu", "Cắt tóc", "Gội thư giãn" }, model.Groups.SelectMany(g => g.Services).Select(s => s.ServiceName));
    }

    private static ServicesController Controller(ApplicationDbContext db) => new(db)
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext() }
    };

    private static async Task<ApplicationDbContext> CreateDb()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
        var stylist = new Stylist();
        Service Available(string name) => new() { ServiceName = name, Stylists = [new() { Stylist = stylist }] };
        var stopped = Available("Cắt tóc ngừng bán");
        stopped.IsActive = false;
        db.AddRange(
            new ServiceGroup { GroupName = "Tóc", DisplayOrder = 2, Services = [Available("Cắt tóc"), stopped,
                new() { ServiceName = "Cắt tóc chưa có thợ" },
                new() { ServiceName = "Cắt tóc thợ nghỉ", Stylists = [new() { Stylist = new Stylist { IsActive = false } }] }] },
            new ServiceGroup { GroupName = "Gội", DisplayOrder = 1, Services = [Available("Gội đầu")] },
            Available("Gội thư giãn"));
        await db.SaveChangesAsync();
        return db;
    }
}
