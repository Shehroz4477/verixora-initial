using Identity.Application;
using Microsoft.Extensions.Hosting;

namespace Identity.Infrastructure;

/// <summary>
/// Local-only delivery adapters. Production must replace these with a managed
/// SMS and email provider before the environment can start serving users.
/// </summary>
public sealed class LocalDevelopmentSmsService(IHostEnvironment environment) : ISmsService
{
    public Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        EnsureDevelopment(environment);
        Console.WriteLine($"LOCAL DEVELOPMENT SMS to {phoneNumber}: OTP {code}");
        return Task.CompletedTask;
    }

    private static void EnsureDevelopment(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("A production SMS provider is required outside the Development environment.");
    }
}

public sealed class LocalDevelopmentEmailService(IHostEnvironment environment) : IEmailService
{
    public Task SendVerificationCodeAsync(string email, string code)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("A production email provider is required outside the Development environment.");

        Console.WriteLine($"LOCAL DEVELOPMENT EMAIL to {email}: OTP {code}");
        return Task.CompletedTask;
    }
}
