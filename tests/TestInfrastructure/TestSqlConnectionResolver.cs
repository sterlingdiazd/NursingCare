using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace NursingCareBackend.Tests.Infrastructure;

public static class TestSqlConnectionResolver
{
  private const string EnvironmentVariableName = "NursingCare_TestSqlConnection";
  private static readonly object SweepGate = new();
  private static bool _sweptStaleDatabases;

  public static string GetBaseConnectionString()
  {
    var existing = Environment.GetEnvironmentVariable(EnvironmentVariableName);
    if (!string.IsNullOrWhiteSpace(existing))
    {
      return existing;
    }

    var resolved = BuildConnectionStringFromBackendEnv() ?? BuildFallbackConnectionString();
    Environment.SetEnvironmentVariable(EnvironmentVariableName, resolved);
    SweepStaleTestDatabasesOnce(resolved);
    return resolved;
  }

  /// <summary>
  /// Self-healing guard against leaked test databases. Each test spins up a unique
  /// <c>NursingCareDb_Test_run_*</c> database and drops it on teardown — but a killed or
  /// crashed run (Ctrl-C, a failed pre-push hook, an OOM) never reaches teardown, so its
  /// database leaks. Left unchecked these pile into the thousands and exhaust SQL Server's
  /// 'internal' memory resource pool (Error 701), after which every new connection fails its
  /// pre-login handshake and the whole suite goes red for reasons that have nothing to do with
  /// the code under test. Once per test process we drop any test database older than the
  /// current run, so leaks can never accumulate again. Scoped to stale (&gt;30 min) databases
  /// so a concurrently-running suite's fresh databases are never touched, and best-effort so a
  /// cleanup hiccup can never fail a test run.
  /// </summary>
  private static void SweepStaleTestDatabasesOnce(string baseConnectionString)
  {
    lock (SweepGate)
    {
      if (_sweptStaleDatabases)
      {
        return;
      }

      _sweptStaleDatabases = true;
    }

    try
    {
      var masterConnectionString = Regex.IsMatch(baseConnectionString, @"Database\s*=\s*[^;]+", RegexOptions.IgnoreCase)
        ? Regex.Replace(baseConnectionString, @"Database\s*=\s*[^;]+", "Database=master", RegexOptions.IgnoreCase)
        : $"{baseConnectionString.TrimEnd(';')};Database=master";

      using var connection = new SqlConnection(masterConnectionString);
      connection.Open();

      using var command = connection.CreateCommand();
      command.CommandTimeout = 180;
      command.CommandText = @"
SET NOCOUNT ON;
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'ALTER DATABASE [' + name + N'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [' + name + N'];'
FROM sys.databases
WHERE name LIKE 'NursingCareDb[_]Test[_]run[_]%'
  AND create_date < DATEADD(MINUTE, -30, GETUTCDATE());
IF LEN(@sql) > 0 EXEC sp_executesql @sql;";
      command.ExecuteNonQuery();
    }
    catch
    {
      // Best-effort: never fail a test run because the stale-database sweep could not run.
    }
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
