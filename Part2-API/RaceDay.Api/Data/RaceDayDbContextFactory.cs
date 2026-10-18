using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RaceDay.Api.Data;

// Used only by the EF Core command line tools, when they create migrations
// or update the database. At design time there is no running web host, so
// the tools build the context from appsettings.json through this factory.
public class RaceDayDbContextFactory : IDesignTimeDbContextFactory<RaceDayDbContext>
{
    public RaceDayDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseSqlServer(configuration.GetConnectionString("RaceDayDb"))
            .Options;

        return new RaceDayDbContext(options);
    }
}
