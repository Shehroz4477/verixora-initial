using BuildingBlocks.Domain;

namespace Homes.Domain;

public class HomeMember : Entity
{
    public Guid UserId { get; private set; }
    public Guid HomeId { get; private set; }
    public HomeMemberRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    // Navigation back to Home
    public Home Home { get; private set; } = null!;

    // EF Core parameterless constructor
    private HomeMember()
    {
    }

    public HomeMember(Guid userId, Guid homeId, HomeMemberRole role)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        HomeId = homeId;
        Role = role;
        JoinedAt = DateTime.UtcNow;
    }
}
