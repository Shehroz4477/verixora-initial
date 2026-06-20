namespace Identity.Application;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string email, string code);
}
