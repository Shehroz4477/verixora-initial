using AuditLogs.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AuditLogs.Presentation;

[ApiController]
[Route("api/v1/auditlogs")]
public class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] Guid homeId)
    {
        // For now, just use a query to get logs; we'll need a Query and Handler.
        // Let's add a simple endpoint that calls a GetAuditLogsQuery later.
        // For immediate demo, return Ok("Not yet implemented – use database view");
        // We'll implement quickly:
        var query = new GetAuditLogsQuery(homeId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
