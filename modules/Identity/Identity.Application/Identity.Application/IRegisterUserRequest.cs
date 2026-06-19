namespace Identity.Application;

public record RegisterUserRequest(
    string PhoneNumber,
    string Password,
    string ConfirmPassword,
    string? Email = null
);
