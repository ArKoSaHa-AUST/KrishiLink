using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KrishiLink.DAL;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(root)
            .AddJsonFile("appsettings.json", optional: false);
        EnvironmentConfiguration.AddLocalEnvironmentFiles(configuration, root, args);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DatabaseConfiguration.GetConnectionString(configuration.Build(), forMigrations: true),
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.Schema));
        return new ApplicationDbContext(options.Options);
    }
}
