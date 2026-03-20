using System.Text.RegularExpressions;

namespace NursingCareBackend.Tests.Infrastructure;

public static class TestSqlConnectionResolver
{
  private const string EnvironmentVariableName = "NursingCare_TestSqlConnection";

  public static string GetBaseConnectionString()
  {
    var existing = Environment.GetEnvironmentVariable(EnvironmentVariableName);
    if (!string.IsNullOrWhiteSpace(existing))
    {
      return existing;
    }

    var resolved = BuildConnectionStringFromBackendEnv() ?? BuildFallbackConnectionString();
    Environment.SetEnvironmentVariable(EnvironmentVariableName, resolved);
    return resolved;
  }

  public static string CreateUniqueDatabaseConnectionString()
  {
    var baseConnectionString = GetBaseConnectionString();
    var uniqueDbName = $"NursingCareDb_Test_run_{Guid.NewGuid():N}";

    if (Regex.IsMatch(baseConnectionString, @"Database\s*=\s*[^;]+", RegexOptions.IgnoreCase))
    {
      return Regex.Replace(
        baseConnectionString,
        @"Database\s*=\s*[^;]+",
        $"Database={uniqueDbName}",
        RegexOptions.IgnoreCase);
    }

    return $"{baseConnectionString.TrimEnd(';')};Database={uniqueDbName}";
  }

  private static string? BuildConnectionStringFromBackendEnv()
  {
    var backendEnvPath = FindBackendEnvPath();
    if (backendEnvPath is null || !File.Exists(backendEnvPath))
    {
      return null;
    }

    var values = File.ReadAllLines(backendEnvPath)
      .Select(line => line.Trim())
      .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal))
      .SelectMany(line =>
      {
        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
          return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');
        return new[] { new KeyValuePair<string, string>(key, value) };
      })
      .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    var host = GetValue(values, "DB_HOST", "localhost");
    var port = GetValue(values, "DB_PORT", "1433");
    var database = GetValue(values, "DB_NAME", "NursingCareDb_Test");
    var user = GetValue(values, "DB_USER", "sa");
    var password = GetValue(values, "DB_PASSWORD", "YourStrong!Passw0rd");

    return $"Server={host},{port};Database={database};User Id={user};Password={password};TrustServerCertificate=True;";
  }

  private static string? FindBackendEnvPath()
  {
    var searchRoots = new[]
    {
      AppContext.BaseDirectory,
      Directory.GetCurrentDirectory(),
    };

    foreach (var root in searchRoots)
    {
      var directory = new DirectoryInfo(root);

      while (directory is not null)
      {
        var candidatePaths = new[]
        {
          Path.Combine(directory.FullName, ".env"),
          Path.Combine(directory.FullName, "NursingCareBackend", ".env"),
        };

        var match = candidatePaths.FirstOrDefault(File.Exists);
        if (match is not null)
        {
          return match;
        }

        directory = directory.Parent;
      }
    }

    return null;
  }

  private static string BuildFallbackConnectionString()
    => "Server=localhost,1433;Database=NursingCareDb_Test;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";

  private static string GetValue(
    IReadOnlyDictionary<string, string> values,
    string key,
    string fallback)
    => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
      ? value
      : fallback;
}
