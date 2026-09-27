using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Teta.Ippcms.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> (migrations add / database update). The connection string comes from the
/// <c>--connection</c> argument or the <c>ConnectionStrings__DefaultConnection</c> environment variable;
/// a local placeholder is used only for generating migrations (no database access needed).
/// </summary>
public sealed class TetaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TetaDbContext>
{
    public TetaDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                         ?? "Server=(localdb)\\mssqllocaldb;Database=IfwemsDb;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<TetaDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", TetaDbContext.Schema))
            .Options;
        return new TetaDbContext(options);
    }
}
