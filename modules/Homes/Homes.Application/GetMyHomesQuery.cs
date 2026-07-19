using MediatR;

namespace Homes.Application;

public sealed record GetMyHomesQuery(Guid UserId) : IRequest<IReadOnlyList<HomeSummary>>;

public sealed class GetMyHomesQueryHandler(IHomeRepository repository)
    : IRequestHandler<GetMyHomesQuery, IReadOnlyList<HomeSummary>>
{
    public Task<IReadOnlyList<HomeSummary>> Handle(GetMyHomesQuery request, CancellationToken cancellationToken)
        => repository.GetForUserAsync(request.UserId, cancellationToken);
}
