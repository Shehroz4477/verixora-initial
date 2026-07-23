using BuildingBlocks.Domain;
using MediatR;

namespace Identity.Application;

/// <summary>
/// Returns the authenticated user's persisted web-access email state. The
/// mobile client uses this to restore the verification flow after navigation,
/// an application restart, or a successful verification.
/// </summary>
public sealed record GetEmailStatusQuery(Guid UserId) : IRequest<EmailStatusResult>;

public sealed record EmailStatusResult(string? Email, bool IsVerified);

public sealed class GetEmailStatusQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetEmailStatusQuery, EmailStatusResult>
{
    public async Task<EmailStatusResult> Handle(GetEmailStatusQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new DomainException("User not found.");

        return new EmailStatusResult(user.Email, user.EmailVerified);
    }
}
