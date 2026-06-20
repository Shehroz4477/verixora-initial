using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class SetEmailCommandHandler : IRequestHandler<SetEmailCommand, SetEmailResult>
{
    private readonly IUserRepository _userRepository;

    public SetEmailCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<SetEmailResult> Handle(SetEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new DomainException("User not found.");

        user.SetEmail(request.Email);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new SetEmailResult(true, "Email set successfully. Please verify it.");
    }
}
