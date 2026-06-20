namespace Identity.Application;

public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string phoneNumber, string role);
}
