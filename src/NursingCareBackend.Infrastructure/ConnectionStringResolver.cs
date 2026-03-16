// NursingCareBackend.Infrastructure/ConnectionStringResolver.cs
namespace NursingCareBackend.Infrastructure;

public static class ConnectionStringResolver
{
  public static string Resolve(string connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
      throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    if (!connectionString.Contains("{SQL_PASSWORD}", StringComparison.Ordinal))
      return connectionString;

    var sqlPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD");

    if (string.IsNullOrEmpty(sqlPassword))
      throw new InvalidOperationException(
          "Connection string contains {SQL_PASSWORD} placeholder but SQL_PASSWORD environment variable is not set.");

    return connectionString.Replace("{SQL_PASSWORD}", sqlPassword, StringComparison.Ordinal);
  }
}