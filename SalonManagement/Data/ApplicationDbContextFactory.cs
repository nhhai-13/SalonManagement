using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
 codex/sprint-4-todo

using Microsoft.AspNetCore.Http;
develop

namespace SalonManagement.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SalonManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        if (string.Equals(Environment.GetEnvironmentVariable("DatabaseProvider"), "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
 codex/sprint-4-todo
        var options = optionsBuilder.Options;
        return new ApplicationDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor());


        return new ApplicationDbContext(optionsBuilder.Options, new HttpContextAccessor());
 develop
    }
}
