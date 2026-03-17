using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Authentication;

public interface ITokenGenerator
{
    string GenerateToken(User user);
}
