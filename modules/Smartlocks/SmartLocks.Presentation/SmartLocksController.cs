using BuildingBlocks.Domain;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartLocks.Application;

namespace SmartLocks.Presentation;

[ApiController]
[Route("api/v1/locks")]
public class SmartLocksController : ControllerBase
{
    private readonly IMediator _mediator;

    public SmartLocksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterSmartLock([FromBody] RegisterSmartLockCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
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
            var command = new UnlockDoorCommand(
                lockId,
                userId,
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

    private Guid GetUserIdFromToken()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim.Value) : Guid.Empty;
    }
}

public class UnlockRequest
{
    public IFormFile? FaceImage { get; set; }
    public string? IdempotencyKey { get; set; }
}
