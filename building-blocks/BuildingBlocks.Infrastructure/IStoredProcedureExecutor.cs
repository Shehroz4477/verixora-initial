using System.Data;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Common boundary for invoking an approved database routine. Implementations
/// must use parameters; arbitrary client-provided SQL is intentionally absent.
/// </summary>
public interface IStoredProcedureExecutor
{
    Task<int> ExecuteAsync(
        string routineName,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}
