using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Identity.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Infrastructure;

/// <summary>
/// Registers local console delivery only in Development. Every other environment
/// must declare real managed providers before the API will start.
/// </summary>
public static class MessagingProviderRegistration
{
    public static IServiceCollection AddVerixoraMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            services.AddSingleton<ISmsService, LocalDevelopmentSmsService>();
            services.AddSingleton<IEmailService, LocalDevelopmentEmailService>();
            return services;
        }

        RequireProvider(configuration, "Messaging:Sms:Provider", "Twilio", "SMS");
        RequireSetting(configuration, "Messaging:Sms:Twilio:AccountSid");
        RequireSetting(configuration, "Messaging:Sms:Twilio:AuthToken");
        RequireSetting(configuration, "Messaging:Sms:Twilio:FromNumber");
        RequireProvider(configuration, "Messaging:Email:Provider", "SendGrid", "email");
        RequireSetting(configuration, "Messaging:Email:SendGrid:ApiKey");
        RequireSetting(configuration, "Messaging:Email:SendGrid:FromEmail");

        services.AddHttpClient<ISmsService, TwilioSmsService>(client =>
        {
            client.BaseAddress = new Uri("https://api.twilio.com/2010-04-01/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<IEmailService, SendGridEmailService>(client =>
        {
            client.BaseAddress = new Uri("https://api.sendgrid.com/v3/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    private static void RequireProvider(IConfiguration configuration, string key, string expected, string description)
    {
        var configured = configuration[key];
        if (!string.Equals(configured, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Production {description} delivery requires Messaging configuration with Provider '{expected}'.");
    }

    private static void RequireSetting(IConfiguration configuration, string key)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
            throw new InvalidOperationException($"Production message delivery requires configuration value '{key}'.");
    }
}

public sealed class TwilioSmsService(HttpClient httpClient, IConfiguration configuration) : ISmsService
{
    public async Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        var accountSid = Required(configuration, "Messaging:Sms:Twilio:AccountSid");
        var authToken = Required(configuration, "Messaging:Sms:Twilio:AuthToken");
        var fromNumber = Required(configuration, "Messaging:Sms:Twilio:FromNumber");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"Accounts/{Uri.EscapeDataString(accountSid)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = phoneNumber,
                ["From"] = fromNumber,
                ["Body"] = $"Your Verixora verification code is {code}."
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}")));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string Required(IConfiguration configuration, string key)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing required message-delivery setting '{key}'.");
}

public sealed class SendGridEmailService(HttpClient httpClient, IConfiguration configuration) : IEmailService
{
    public async Task SendVerificationCodeAsync(string email, string code)
    {
        var apiKey = Required(configuration, "Messaging:Email:SendGrid:ApiKey");
        var fromEmail = Required(configuration, "Messaging:Email:SendGrid:FromEmail");
        var fromName = configuration["Messaging:Email:SendGrid:FromName"] ?? "Verixora";
        using var request = new HttpRequestMessage(HttpMethod.Post, "mail/send")
        {
            Content = JsonContent.Create(new
            {
                personalizations = new[] { new { to = new[] { new { email } } } },
                from = new { email = fromEmail, name = fromName },
                subject = "Your Verixora verification code",
                content = new[] { new { type = "text/plain", value = $"Your Verixora verification code is {code}." } }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static string Required(IConfiguration configuration, string key)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing required message-delivery setting '{key}'.");
}
