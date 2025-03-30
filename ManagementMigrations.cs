using System.Data;
using Dapper;
using System.Text.RegularExpressions;

public class ManagementMigrations
{
    private readonly IDbConnection _connection;
    private readonly string _folderMigrations;

    public ManagementMigrations(IDbConnection connection, string folderMigrations)
    {
        _connection = connection;
        _folderMigrations = folderMigrations;
    }

    private async Task CreateNextScript()
    {
        // Get the highest ID from executed migrations
        var result = await _connection.QueryFirstOrDefaultAsync<int>(
            "SELECT MAX(id) FROM management_migrations"
        );

        int nextNumber = (result == 0) ? 1 : result + 1;
        string nextScriptName = $"{nextNumber}_proximo.sql";
        string nextScriptPath = Path.Combine(_folderMigrations, nextScriptName);

        // Create the new script file with a basic template
        string template = "-- Novo script de migração\n-- Data de criação: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n";
        await File.WriteAllTextAsync(nextScriptPath, template);

        Console.WriteLine($"Criado novo script: {nextScriptName}");
    }

    public async Task ExecutePendingMigrations()
    {
        // Ensure the migrations table exists
        await CreateTableManagementMigrationsSqlServer();

        // Get all SQL files from the migrations folder
        var migrationFiles = Directory.GetFiles(_folderMigrations, "*.sql")
            .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d+"))
            .OrderBy(f => int.Parse(Regex.Match(Path.GetFileName(f), @"^\d+").Value))
            .ToList();

        // Get executed migrations from the database
        var executedMigrations = await _connection.QueryAsync<string>(
            "SELECT scriptName FROM management_migrations"
        );

        foreach (var migrationFile in migrationFiles)
        {
            var scriptName = Path.GetFileName(migrationFile);

            // Skip if migration has already been executed
            if (executedMigrations.Contains(scriptName))
                continue;

            try
            {
                // Read and execute the SQL script
                var sqlScript = await File.ReadAllTextAsync(migrationFile);
                await _connection.ExecuteAsync(sqlScript);

                // Record the successful migration
                await _connection.ExecuteAsync(
                    "INSERT INTO management_migrations (id, name, scriptName, createdAt) VALUES (@Id, @Name, @ScriptName, @CreatedAt)",
                    new
                    {
                        Id = int.Parse(Regex.Match(scriptName, @"^\d+").Value),
                        Name = Path.GetFileNameWithoutExtension(scriptName),
                        ScriptName = scriptName,
                        CreatedAt = DateTime.Now
                    }
                );

                Console.WriteLine($"Successfully executed migration: {scriptName}");

                // Create the next script after all migrations are executed
                //await CreateNextScript();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing migration {scriptName}: {ex.Message}");
                throw; // Rethrow to stop the migration process
            }
        }

    }

    public async Task CreateTableManagementMigrationsMysql()
    {
        await _connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS management_migrations (id INT PRIMARY KEY, name VARCHAR(255) NOT NULL,  scriptName VARCHAR(255) NOT NULL, createdAt DATETIME NOT NULL)");
    }
    public async Task CreateTableManagementMigrationsPostgres()
    {
        await _connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS management_migrations (id INT PRIMARY KEY, name VARCHAR(255) NOT NULL,  scriptName VARCHAR(255) NOT NULL, createdAt DATETIME NOT NULL)");
    }
    public async Task CreateTableManagementMigrationsSqlite()
    {
        await _connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS management_migrations (id INT PRIMARY KEY, name VARCHAR(255) NOT NULL,  scriptName VARCHAR(255) NOT NULL, createdAt DATETIME NOT NULL)");
    }
    public async Task CreateTableManagementMigrationsSqlServer()
    {
        await _connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS management_migrations (id INT PRIMARY KEY, name VARCHAR(255) NOT NULL,  scriptName VARCHAR(255) NOT NULL, createdAt DATETIME NOT NULL)");
    }

}


