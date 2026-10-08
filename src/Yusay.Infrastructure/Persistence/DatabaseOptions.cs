namespace Yusay.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
    public string DefaultSchema { get; set; } = "yusay";
}
