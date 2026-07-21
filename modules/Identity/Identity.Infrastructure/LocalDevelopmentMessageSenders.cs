using Identity.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure;

/// <summary>
/// Local-only delivery adapters. Production must replace these with a managed
/// SMS and email provider before the environment can start serving users.
/// </summary>
public sealed class LocalDevelopmentSmsService(
    IHostEnvironment environment,
    ILogger<LocalDevelopmentSmsService> logger) : ISmsService
{
    public Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        EnsureDevelopment(environment);
        logger.LogWarning("LOCAL DEVELOPMENT SMS to {PhoneNumber}: OTP {Otp}", phoneNumber, code);
        return Task.CompletedTask;
    }

    private static void EnsureDevelopment(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("A production SMS provider is required outside the Development environment.");
    }
}

public sealed class LocalDevelopmentEmailService(
    IHostEnvironment environment,
    ILogger<LocalDevelopmentEmailService> logger) : IEmailService
{
    public Task SendVerificationCodeAsync(string email, string code)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("A production email provider is required outside the Development environment.");

        logger.LogWarning("LOCAL DEVELOPMENT EMAIL to {Email}: OTP {Otp}", email, code);
        return Task.CompletedTask;
    }
}
