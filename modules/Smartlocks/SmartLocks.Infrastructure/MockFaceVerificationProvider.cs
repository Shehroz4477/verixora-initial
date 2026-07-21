using FaceVerification.Domain;

namespace SmartLocks.Infrastructure;

public class MockFaceVerificationProvider : IFaceVerificationProvider
{
    public Task<bool> VerifyAsync(Guid userId, Stream imageStream, CancellationToken cancellationToken = default)
    {
        // Always return true for demo – real provider would call Python
        Console.WriteLine($"FACE VERIFY: User={userId} – Mock always returns true");
        return Task.FromResult(true);
    }

    public Task EnrollAsync(Guid userId, List<Stream> imageStreams, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"FACE ENROLL: User={userId} – Mock enrollment successful");
        return Task.CompletedTask;
    }

    public Task DeleteEnrollmentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"FACE DELETE: User={userId} - Mock enrollment removed");
        return Task.CompletedTask;
    }
}
