namespace SmartLocks.Domain;

public enum LockCommandStatus
{
    Queued,
    Published,
    Acknowledged,
    Failed,
    Expired
}
