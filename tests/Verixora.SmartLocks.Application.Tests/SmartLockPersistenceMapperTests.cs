using SmartLocks.Domain;
using SmartLocks.Infrastructure;
using Xunit;

namespace Verixora.SmartLocks.Application.Tests;

public sealed class SmartLockPersistenceMapperTests
{
    [Fact]
    public void Create_parameters_match_the_cross_provider_create_routine_contract()
    {
        var smartLock = new SmartLock("Front door", Guid.NewGuid(), Guid.NewGuid(), requiresFace: true);

        var parameterNames = PersistedSmartLock.ToCreateParameters(smartLock)
            .GetType()
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            new[] { "DeviceId", "HomeId", "Id", "Name", "RequiresFace", "Status" },
            parameterNames);
    }
}
