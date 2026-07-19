namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Chooses the server-side data-access implementation. This is deployment and
/// developer configuration only; it must never be supplied by an API client.
/// </summary>
public enum DataAccessMode
{
    DapperStoredProcedure,
    AdoNetStoredProcedure,
    EfCore
}
