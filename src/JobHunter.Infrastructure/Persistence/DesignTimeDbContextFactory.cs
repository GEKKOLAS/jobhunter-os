using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobHunter.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` at design time; generating migrations needs no live database.
/// Override the connection string with JH_DESIGNTIME_CONNECTION when scripting against a server.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("JH_DESIGNTIME_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=jobhunter;Username=jh;Password=jh_dev_password";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
