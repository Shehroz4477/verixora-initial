using BuildingBlocks.Domain;

namespace Identity.Domain;

public sealed class EmailVerifiedDomainEvent : IDomainEvent
{
    public Guid UserId { get; }
    public string Email { get; }
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public EmailVerifiedDomainEvent(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}
