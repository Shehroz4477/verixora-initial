using AuditLogs.Application;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuditLogs.Presentation;

[ApiController]
[Route("api/v1/auditlogs")]
[Authorize]
public sealed class AuditLogsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] Guid homeId, CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetAuditLogsQuery(homeId, GetCurrentUserId(), IsSystemAdmin());
            return Ok(await mediator.Send(query, cancellationToken));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");
        return userId;
    }

    private bool IsSystemAdmin()
        => string.Equals(User.FindFirstValue(ClaimTypes.Role), "SystemAdmin", StringComparison.Ordinal);
}
