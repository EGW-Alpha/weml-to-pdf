using Egw.PubManagement.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PdfGenerator;

public class PubDevContext : PublicationDbContext
{
    private readonly string? _connectionString;

    public PubDevContext(DbContextOptions<PublicationDbContext> options, string? connectionString = null) : base(options, connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_connectionString is not null)
        {
            optionsBuilder
                .UseNpgsql(_connectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .EnableSensitiveDataLogging()
                .UseSnakeCaseNamingConvention();
        }
        optionsBuilder.UseSnakeCaseNamingConvention();
        base.OnConfiguring(optionsBuilder);
    }
}