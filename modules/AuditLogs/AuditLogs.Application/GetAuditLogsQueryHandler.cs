using AuditLogs.Domain;
using BuildingBlocks.Domain;
using Homes.Application;
using MediatR;

namespace AuditLogs.Application;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, List<AuditLog>>
{
    private readonly IAuditLogRepository _repository;
    private readonly IHomeRepository _homeRepository;

    public GetAuditLogsQueryHandler(IAuditLogRepository repository, IHomeRepository homeRepository)
    {
        _repository = repository;
        _homeRepository = homeRepository;
    }

    public async Task<List<AuditLog>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (request.RequestedBy == Guid.Empty)
            throw new DomainException("An authenticated user is required.");

        if (!request.IsSystemAdmin)
        {
            var homes = await _homeRepository.GetForUserAsync(request.RequestedBy, cancellationToken);
            if (!homes.Any(home => home.Id == request.HomeId && string.Equals(home.Role, "Owner", StringComparison.Ordinal)))
                throw new DomainException("Only the home owner can view this audit trail.");
        }

        return await _repository.GetByHomeIdAsync(request.HomeId, cancellationToken);
    }
}
