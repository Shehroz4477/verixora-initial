using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Identity.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Infrastructure;

/// <summary>
/// Registers local console delivery in Development unless real providers have
/// been explicitly opted into. Every other environment must declare managed
/// providers before the API will start.
/// </summary>
public static class MessagingProviderRegistration
{
    public static IServiceCollection AddVerixoraMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var useRealProviders = !environment.IsDevelopment()
            || configuration.GetValue<bool>("Messaging:UseRealProvidersInDevelopment");
        if (!useRealProviders)
        {
            services.AddSingleton<ISmsService, LocalDevelopmentSmsService>();
            services.AddSingleton<IEmailService, LocalDevelopmentEmailService>();
            return services;
        }

        RequireProvider(configuration, "Messaging:Sms:Provider", "Twilio", "SMS");
        RequireSetting(configuration, "Messaging:Sms:Twilio:AccountSid");
        RequireSetting(configuration, "Messaging:Sms:Twilio:AuthToken");
        RequireSetting(configuration, "Messaging:Sms:Twilio:FromNumber");
        services.AddHttpClient<ISmsService, TwilioSmsService>(client =>
        {
            client.BaseAddress = new Uri("https://api.twilio.com/2010-04-01/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        var emailProvider = configuration["Messaging:Email:Provider"];
        if (string.Equals(emailProvider, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            RequireSetting(configuration, "Messaging:Email:SendGrid:ApiKey");
            RequireSetting(configuration, "Messaging:Email:SendGrid:FromEmail");
            services.AddHttpClient<IEmailService, SendGridEmailService>(client =>
            {
                client.BaseAddress = new Uri("https://api.sendgrid.com/v3/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        }
        else if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            RequireSetting(configuration, "Messaging:Email:Smtp:Host");
            RequireSetting(configuration, "Messaging:Email:Smtp:UserName");
            RequireSetting(configuration, "Messaging:Email:Smtp:Password");
            RequireSetting(configuration, "Messaging:Email:Smtp:FromEmail");
            services.AddSingleton<IEmailService, SmtpEmailService>();
        }
        else
        {
            throw new InvalidOperationException("Email delivery requires Messaging:Email:Provider to be 'SendGrid' or 'Smtp'.");
        }

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

/// <summary>
/// Allows a managed SMTP mailbox (for example an organization's Microsoft 365
/// mailbox) to deliver real verification messages. The password must be an app
/// password or provider token loaded from secret configuration, never a source file.
/// </summary>
public sealed class SmtpEmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendVerificationCodeAsync(string email, string code)
    {
        var host = Required(configuration, "Messaging:Email:Smtp:Host");
        var port = configuration.GetValue<int?>("Messaging:Email:Smtp:Port") ?? 587;
        var userName = Required(configuration, "Messaging:Email:Smtp:UserName");
        var password = Required(configuration, "Messaging:Email:Smtp:Password");
        var fromEmail = Required(configuration, "Messaging:Email:Smtp:FromEmail");
        var fromName = configuration["Messaging:Email:Smtp:FromName"] ?? "Verixora";

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Your Verixora verification code",
            Body = $"Your Verixora verification code is {code}. It expires in five minutes.",
            IsBodyHtml = false
        };
        message.To.Add(email);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(userName, password),
            Timeout = 10_000
        };
        await client.SendMailAsync(message);
    }

    private static string Required(IConfiguration configuration, string key)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing required message-delivery setting '{key}'.");
}
