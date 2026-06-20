using BuildingBlocks.Domain;
using Identity.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Presentation;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // Update the existing register endpoint (already there, just ensure it's correct)
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command)
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
}
