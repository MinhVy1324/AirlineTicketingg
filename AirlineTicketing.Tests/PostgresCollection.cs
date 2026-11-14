namespace AirlineTicketing.Tests;

/// <summary>
/// Gom các test cần PostgreSQL thật vào một collection để chúng không chạy song song
/// và giành dữ liệu của nhau trên cùng database test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection
{
    public const string Name = "PostgreSQL";
}
