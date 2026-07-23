using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Domain;
using Identity.Application;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Identity.Infrastructure;

/// <summary>
/// One-time code store with expiry, per-destination resend throttling, bounded
/// attempts, and atomic single-use validation. Redis only contains HMAC values,
/// never a plaintext OTP.
/// </summary>
public sealed class RedisOtpService : IOtpService, IEmailOtpService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaximumAttempts = 5;

    private const string ValidateScript = """
        local expected = redis.call('GET', KEYS[1])
        if not expected then return 0 end
        if expected == ARGV[1] then
            redis.call('DEL', KEYS[1])
            redis.call('DEL', KEYS[2])
            return 1
        end
        local attempts = redis.call('INCR', KEYS[2])
        if attempts >= tonumber(ARGV[2]) then
            redis.call('DEL', KEYS[1])
            redis.call('DEL', KEYS[2])
        end
        return 0
        """;

    private readonly IDatabase _database;
    private readonly byte[] _hashKey;
    private readonly ISmsService _smsService;
    private readonly IEmailService _emailService;

    public RedisOtpService(
        IConnectionMultiplexer connectionMultiplexer,
        IConfiguration configuration,
        ISmsService smsService,
        IEmailService emailService)
    {
        _database = connectionMultiplexer.GetDatabase();
        var secret = configuration["Otp:HashKey"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("Otp:HashKey must be at least 32 characters and supplied through secret configuration.");

        _hashKey = Encoding.UTF8.GetBytes(secret);
        _smsService = smsService;
        _emailService = emailService;
    }

    public async Task SendRegistrationOtpAsync(string phoneNumber)
    {
        await IssueCodeAsync("mobile-registration", phoneNumber, async generatedCode =>
        {
            await _smsService.SendOtpAsync(phoneNumber, generatedCode);
        });
    }

    public Task<bool> ValidateRegistrationOtpAsync(string phoneNumber, string otp)
        => ValidateCodeAsync("mobile-registration", phoneNumber, otp);

    public async Task SendLoginOtpAsync(string phoneNumber)
    {
        await IssueCodeAsync("mobile-login", phoneNumber, async generatedCode =>
        {
            await _smsService.SendOtpAsync(phoneNumber, generatedCode);
        });
    }

    public Task<bool> ValidateLoginOtpAsync(string phoneNumber, string otp)
        => ValidateCodeAsync("mobile-login", phoneNumber, otp);

    public async Task SendEmailOtpAsync(string email)
    {
        await IssueCodeAsync("web-login", email, code => _emailService.SendVerificationCodeAsync(email, code));
    }

    public Task<bool> ValidateEmailOtpAsync(string email, string otp)
        => ValidateCodeAsync("web-login", email, otp);

    public async Task SendEmailVerificationOtpAsync(string email)
    {
        await IssueCodeAsync("email-verification", email, code => _emailService.SendVerificationCodeAsync(email, code));
    }

    public Task<bool> ValidateEmailVerificationOtpAsync(string email, string otp)
        => ValidateCodeAsync("email-verification", email, otp);

    private async Task<string> IssueCodeAsync(string purpose, string destination, Func<string, Task> deliver)
    {
        var subject = SubjectHash(destination);
        var cooldownKey = (RedisKey)$"verixora:otp:cooldown:{purpose}:{subject}";
        var accepted = await _database.StringSetAsync(cooldownKey, "1", ResendCooldown, When.NotExists);
        if (!accepted)
            throw new DomainException("Please wait before requesting another verification code.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otpKey = (RedisKey)$"verixora:otp:value:{purpose}:{subject}";
        var attemptKey = (RedisKey)$"verixora:otp:attempts:{purpose}:{subject}";
        await _database.StringSetAsync(otpKey, CodeHash(code), OtpLifetime);
        await _database.StringSetAsync(attemptKey, "0", OtpLifetime);

        try
        {
            await deliver(code);
            return code;
        }
        catch (Exception exception)
        {
            await _database.KeyDeleteAsync(new RedisKey[] { otpKey, attemptKey, cooldownKey });
            if (exception is DomainException)
                throw;

            // Do not leak a provider response (which can contain account metadata)
            // and do not leave a code in Redis when no message was delivered.
            throw new DomainException("We could not deliver a verification code right now. Please try again shortly.");
        }
    }

    private async Task<bool> ValidateCodeAsync(string purpose, string destination, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsAsciiDigit))
            return false;

        var subject = SubjectHash(destination);
        var result = await _database.ScriptEvaluateAsync(
            ValidateScript,
            new RedisKey[]
            {
                $"verixora:otp:value:{purpose}:{subject}",
                $"verixora:otp:attempts:{purpose}:{subject}"
            },
            new RedisValue[] { CodeHash(code), MaximumAttempts });

        return (int)result == 1;
    }

    private string SubjectHash(string destination)
    {
        var normalized = destination.Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private string CodeHash(string code)
        => Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(code)));
}
