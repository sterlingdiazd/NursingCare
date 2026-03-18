using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Authentication;

public interface ITokenGenerator
{
    TokenResult GenerateToken(User user);
}
