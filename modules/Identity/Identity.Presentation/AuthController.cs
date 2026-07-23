using BuildingBlocks.Domain;
using Identity.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    [HttpPost("access-eligibility")]
    [AllowAnonymous]
    public async Task<IActionResult> AccessEligibility([FromBody] AuthAccessEligibilityQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpCommand command)
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

    [HttpPost("register")]
    [AllowAnonymous]
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

    [HttpPost("send-login-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendLoginOtp([FromBody] SendLoginOtpCommand command)
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

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
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

    [HttpPost("set-email")]
    [Authorize]
    public async Task<IActionResult> SetEmail([FromBody] SetEmailRequest request)
    {
        try
        {
            var command = new SetEmailCommand(GetCurrentUserId(), request.Email);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpGet("email-status")]
    [Authorize]
    public async Task<IActionResult> GetEmailStatus()
    {
        try
        {
            var result = await _mediator.Send(new GetEmailStatusQuery(GetCurrentUserId()));
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpPost("send-verification-email")]
    [Authorize]
    public async Task<IActionResult> SendVerificationEmail()
    {
        try
        {
            var command = new SendEmailVerificationCommand(GetCurrentUserId());
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpPost("verify-email")]
    [Authorize]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        try
        {
            var command = new VerifyEmailCommand(GetCurrentUserId(), request.Code);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpPost("web/send-login-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> WebSendLoginOtp([FromBody] WebLoginSendOtpCommand command)
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

    [HttpPost("web/login")]
    [AllowAnonymous]
    public async Task<IActionResult> WebLogin([FromBody] WebLoginCommand command)
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

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

        return userId;
    }
}

public record SetEmailRequest(string Email);
public record VerifyEmailRequest(string Code);
