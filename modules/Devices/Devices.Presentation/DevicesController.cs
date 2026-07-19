using BuildingBlocks.Domain;
using Devices.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Devices.Presentation;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DevicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        try
        {
            var command = new RegisterDeviceCommand(request.HomeId, request.Name, request.HardwareId, GetCurrentUserId());
            var result = await _mediator.Send(command);
            return Ok(result);
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
}

public sealed record RegisterDeviceRequest(Guid HomeId, string Name, string HardwareId);
