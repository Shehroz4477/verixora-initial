namespace BuildingBlocks.Domain;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}