using AuditLogs.Domain;
using MediatR;

namespace AuditLogs.Application;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, List<AuditLog>>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogsQueryHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<AuditLog>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
        => await _repository.GetByHomeIdAsync(request.HomeId, cancellationToken);
}
