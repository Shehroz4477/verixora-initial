using MediatR;

namespace Homes.Application;

public sealed record GetMyHomesQuery(Guid UserId, bool IsSystemAdmin = false) : IRequest<IReadOnlyList<HomeSummary>>;

public sealed class GetMyHomesQueryHandler(IHomeRepository repository)
    : IRequestHandler<GetMyHomesQuery, IReadOnlyList<HomeSummary>>
{
    public Task<IReadOnlyList<HomeSummary>> Handle(GetMyHomesQuery request, CancellationToken cancellationToken)
        => request.IsSystemAdmin ? repository.GetAllAsync(cancellationToken) : repository.GetForUserAsync(request.UserId, cancellationToken);
}
