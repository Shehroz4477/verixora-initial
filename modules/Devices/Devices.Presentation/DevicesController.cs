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
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetForHome([FromQuery] Guid homeId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new GetControllersForHomeQuery(homeId, GetCurrentUserId(), string.Equals(GetCurrentUserRole(), "SystemAdmin", StringComparison.Ordinal)),
                cancellationToken);
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [AllowAnonymous]
    [HttpPost("provisioning/complete")]
    public async Task<IActionResult> CompleteProvisioning([FromBody] CompleteControllerProvisioningRequest request)
    {
        try { return Ok(await _mediator.Send(new CompleteControllerProvisioningCommand(request.DeviceId, request.ProvisioningToken, request.ControllerPublicKeyPem, request.HardwareAttestation))); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message, code = ex.ErrorCode }); }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

        return userId;
    }

    private string GetCurrentUserRole()
        => User.FindFirstValue(ClaimTypes.Role) ?? "Guest";
}

public sealed record RegisterDeviceRequest(Guid HomeId, string Name, string HardwareId);
public sealed record CompleteControllerProvisioningRequest(Guid DeviceId, string ProvisioningToken, string ControllerPublicKeyPem, string? HardwareAttestation);
