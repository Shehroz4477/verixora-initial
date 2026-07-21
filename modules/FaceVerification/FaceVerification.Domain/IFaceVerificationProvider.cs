namespace FaceVerification.Domain;

public interface IFaceVerificationProvider
{
    Task<bool> VerifyAsync(Guid userId, Stream imageStream, CancellationToken cancellationToken = default);
    Task EnrollAsync(Guid userId, List<Stream> imageStreams, CancellationToken cancellationToken = default);
    Task DeleteEnrollmentAsync(Guid userId, CancellationToken cancellationToken = default);
}
