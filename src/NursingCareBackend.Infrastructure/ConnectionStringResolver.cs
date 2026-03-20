// NursingCareBackend.Infrastructure/ConnectionStringResolver.cs
namespace NursingCareBackend.Infrastructure;

public static class ConnectionStringResolver
{
  public static string Resolve(string connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
      throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    // Replace all environment variable placeholders
    connectionString = ReplaceEnvVariable(connectionString, "DB_SERVER", "localhost,1433");
    connectionString = ReplaceEnvVariable(connectionString, "DB_NAME", "NursingCareDb");
    connectionString = ReplaceEnvVariable(connectionString, "DB_USER", "sa");
    connectionString = ReplaceEnvVariable(connectionString, "DB_PASSWORD", "YourStrong!Passw0rd");
    connectionString = ReplaceEnvVariable(connectionString, "JWT_KEY", "ChangeThisDevelopmentKeyToARealSecret");

    return connectionString;
  }

  private static string ReplaceEnvVariable(string connectionString, string placeholder, string defaultValue)
  {
    var envKey = placeholder;
    var placeholder_key = "{" + placeholder + "}";

    if (!connectionString.Contains(placeholder_key, StringComparison.Ordinal))
      return connectionString;

    var envValue = Environment.GetEnvironmentVariable(envKey);
    var value = string.IsNullOrEmpty(envValue) ? defaultValue : envValue;

    return connectionString.Replace(placeholder_key, value, StringComparison.Ordinal);
  }
}
