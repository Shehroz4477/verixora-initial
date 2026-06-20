using Identity.Application;

namespace Identity.Infrastructure;

public class MockEmailService : IEmailService
{
    public Task SendVerificationCodeAsync(string email, string code)
    {
        Console.WriteLine($"MOCK EMAIL to {email}: Your verification code is {code}");
        return Task.CompletedTask;
    }
}
