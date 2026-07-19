using BuildingBlocks.Domain;
using Homes.Domain;
using Xunit;

namespace Verixora.Domain.Tests;

public sealed class HomeTests
{
    [Fact]
    public void New_home_creates_the_owner_membership_atomically()
    {
        var ownerId = Guid.NewGuid();

        var home = new Home("Main Home", ownerId);

        var membership = Assert.Single(home.Members);
        Assert.Equal(ownerId, membership.UserId);
        Assert.Equal(home.Id, membership.HomeId);
        Assert.Equal(HomeMemberRole.Owner, membership.Role);
    }

    [Fact]
    public void Duplicate_home_member_is_rejected()
    {
        var ownerId = Guid.NewGuid();
        var home = new Home("Main Home", ownerId);

        var exception = Assert.Throws<DomainException>(() => home.AddMember(ownerId, HomeMemberRole.Guest));

        Assert.Equal("User is already a member of this home.", exception.Message);
    }

    [Fact]
    public void Device_limit_cannot_be_set_below_one()
    {
        var home = new Home("Main Home", Guid.NewGuid());

        Assert.Throws<DomainException>(() => home.SetMaxDevices(0));
    }
}
