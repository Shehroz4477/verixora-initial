using System.Security.Claims;
using Homes.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homes.Presentation;

[ApiController, Authorize, Route("api/v1/homes")]
public sealed class HomesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<HomeSummary>> Create(CreateHomeRequest request, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new CreateHomeCommand(CurrentUserId(), request.Name), cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HomeSummary>>> GetMine(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyHomesQuery(CurrentUserId(), IsSystemAdmin()), cancellationToken));

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
    private bool IsSystemAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "SystemAdmin", StringComparison.Ordinal);
}

public sealed record CreateHomeRequest(string Name);
