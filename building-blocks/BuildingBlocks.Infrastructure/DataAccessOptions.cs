namespace BuildingBlocks.Infrastructure;

public sealed class DataAccessOptions
{
    public const string SectionName = "DataAccess";

    /// <summary>
    /// Dapper with parameterized database routines is the production default.
    /// </summary>
    public DataAccessMode Mode { get; init; } = DataAccessMode.DapperStoredProcedure;
}
