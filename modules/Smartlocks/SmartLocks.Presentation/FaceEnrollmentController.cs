using BuildingBlocks.Domain;
using FaceVerification.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SmartLocks.Presentation;

[ApiController]
[Route("api/v1/face")]
[Authorize]
public sealed class FaceEnrollmentController(IFaceVerificationProvider faceVerificationProvider) : ControllerBase
{
    private const int MinimumImages = 3;
    private const int MaximumImages = 5;
    private const long MaximumImageBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    [HttpPost("enroll")]
    [RequestSizeLimit(MaximumImages * MaximumImageBytes)]
    public async Task<IActionResult> Enroll([FromForm] FaceEnrollmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            if (request.Images.Count is < MinimumImages or > MaximumImages)
                throw new DomainException("Provide three to five face images for enrollment.");

            if (request.Images.Any(image => image.Length is <= 0 or > MaximumImageBytes || !AllowedContentTypes.Contains(image.ContentType)))
                throw new DomainException("Each face image must be a JPEG or PNG no larger than 5 MB.");

            var streams = request.Images.Select(image => image.OpenReadStream()).ToList();
            try
            {
                await faceVerificationProvider.EnrollAsync(userId, streams, cancellationToken);
            }
            finally
            {
                foreach (var stream in streams)
                    await stream.DisposeAsync();
            }

            return Ok(new { status = "enrolled", acceptedFrames = request.Images.Count });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Face verification service is unavailable. Enrollment was not saved." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Face verification service timed out. Enrollment was not saved." });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : throw new DomainException("Authenticated user identity is invalid.");
    }
}

public sealed class FaceEnrollmentRequest
{
    public List<IFormFile> Images { get; init; } = [];
}
