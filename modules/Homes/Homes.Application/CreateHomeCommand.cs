using Homes.Domain;
using MediatR;

namespace Homes.Application;

public sealed record CreateHomeCommand(Guid OwnerId, string Name) : IRequest<HomeSummary>;

public sealed class CreateHomeCommandHandler(IHomeRepository repository)
    : IRequestHandler<CreateHomeCommand, HomeSummary>
{
    public Task<HomeSummary> Handle(CreateHomeCommand request, CancellationToken cancellationToken)
    {
        var home = new Home(request.Name, request.OwnerId);
        return repository.AddAsync(home, cancellationToken);
    }
}
