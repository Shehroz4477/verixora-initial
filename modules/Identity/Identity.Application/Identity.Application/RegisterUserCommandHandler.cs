using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Validate password match
        if (request.Password != request.ConfirmPassword)
            throw new DomainException("Passwords do not match.");

        // Check uniqueness of phone number
        if (await _userRepository.PhoneNumberExistsAsync(request.PhoneNumber, cancellationToken))
            throw new DomainException("Phone number already registered.");

        // Hash password
        var passwordHash = _passwordHasher.Hash(request.Password);

        // Create user
        var user = new User(request.PhoneNumber, passwordHash);

        // Set email if provided
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.SetEmail(request.Email);

        // Save
        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id, "User registered successfully.");
    }
}
