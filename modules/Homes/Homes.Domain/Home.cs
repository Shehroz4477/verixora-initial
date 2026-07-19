using BuildingBlocks.Domain;

namespace Homes.Domain;

public class Home : Entity, IAggregateRoot
{
    public string Name { get; private set; }
    public Guid OwnerId { get; private set; }   // User ID who created it
    public DateTime CreatedAt { get; private set; }
    public int MaxDevices { get; private set; }

    private readonly List<HomeMember> _members = new();
    public IReadOnlyCollection<HomeMember> Members => _members.AsReadOnly();

    // EF Core parameterless constructor
    private Home()
    {
        Name = null!;
        // OwnerId stays default Guid.Empty
    }

    public Home(string name, Guid ownerId)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        OwnerId = ownerId;
        CreatedAt = DateTime.UtcNow;
        MaxDevices = 20; // default device limit
        _members.Add(new HomeMember(ownerId, Id, HomeMemberRole.Owner));
    }

    public void AddMember(Guid userId, HomeMemberRole role)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new DomainException("User is already a member of this home.");

        var member = new HomeMember(userId, Id, role);
        _members.Add(member);
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            throw new DomainException("User is not a member of this home.");
        _members.Remove(member);
    }

    public void SetMaxDevices(int maxDevices)
    {
        if (maxDevices < 1)
            throw new DomainException("Max devices must be at least 1.");
        MaxDevices = maxDevices;
    }
}
