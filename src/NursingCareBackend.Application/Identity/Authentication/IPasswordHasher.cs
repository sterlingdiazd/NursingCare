namespace NursingCareBackend.Application.Identity.Authentication;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
