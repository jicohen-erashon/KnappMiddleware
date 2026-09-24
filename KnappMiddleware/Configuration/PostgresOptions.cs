namespace KnappMiddleware.Configuration;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public required string ConnectionString { get; set; }
}
