using System.Security.Claims;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartLocks.Application;

namespace SmartLocks.Presentation;

[ApiController]
[Route("api/v1/locks")]
[Authorize]
public class SmartLocksController : ControllerBase
{
    private readonly IMediator _mediator;

    public SmartLocksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterSmartLock([FromBody] RegisterSmartLockRequest request)
    {
        try
        {
            var command = new RegisterSmartLockCommand(request.Name, request.DeviceId, request.HomeId, GetUserIdFromToken(), request.RequiresFace);
            var result = await _mediator.Send(command);
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
            return Ok(await _mediator.Send(new GetSmartLocksForHomeQuery(homeId, GetUserIdFromToken(), string.Equals(GetUserRoleFromToken(), "SystemAdmin", StringComparison.Ordinal)), cancellationToken));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpPost("{lockId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid lockId, [FromForm] UnlockRequest request)
    {
        try
        {
            var userId = GetUserIdFromToken();
            var userRole = GetUserRoleFromToken();
            var command = new UnlockDoorCommand(
                lockId,
                userId,
                userRole,
                request.FaceImage?.OpenReadStream(),
                request.IdempotencyKey ?? Guid.NewGuid().ToString()
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [AllowAnonymous]
    [HttpPost("controller-acknowledgements")]
    public async Task<IActionResult> AcknowledgeController([FromBody] ControllerAcknowledgementRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _mediator.Send(new AcknowledgeControllerCommand(request.DeviceId, request.CommandId, request.Outcome, request.OccurredAtUtc, request.Nonce, request.SignatureBase64, request.Details), cancellationToken));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    private Guid GetUserIdFromToken()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim.Value) : Guid.Empty;
    }

    private string GetUserRoleFromToken()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.Role);
        return claim?.Value ?? "Guest";
    }
}

public class UnlockRequest
{
    public IFormFile? FaceImage { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed record RegisterSmartLockRequest(string Name, Guid DeviceId, Guid HomeId, bool RequiresFace = false);
public sealed record ControllerAcknowledgementRequest(Guid DeviceId, Guid CommandId, string Outcome, DateTime OccurredAtUtc, string Nonce, string SignatureBase64, string? Details);
