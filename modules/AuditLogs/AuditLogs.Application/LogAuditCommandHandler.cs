using AuditLogs.Domain;
using MediatR;

namespace AuditLogs.Application;

public class LogAuditCommandHandler : IRequestHandler<LogAuditCommand, AuditLog>
{
    private readonly IAuditLogRepository _repository;

    public LogAuditCommandHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<AuditLog> Handle(LogAuditCommand request, CancellationToken cancellationToken)
    {
        var log = new AuditLog(request.HomeId, request.UserId, request.DeviceId, request.Action, request.Result, request.Details);
        await _repository.AddAsync(log, cancellationToken);
        return log;
    }
}
