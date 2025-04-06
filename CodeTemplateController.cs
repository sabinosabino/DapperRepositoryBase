using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using System.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Hosting;

namespace generateCode.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class CodeTemplateController : Controller
{
    private readonly string _connectionString;
    private readonly string[] _csharpTypes = new[]
    {
        "string", "int", "long", "double", "decimal", "bool", "DateTime",
        "Guid", "byte", "char", "float", "short", "uint", "ulong", "ushort"
    };

    // Mapeamento estático de tipos MySQL para C#
    private readonly Dictionary<string, string> _typeMappings = new Dictionary<string, string>
    {
        { "varchar", "string" },
        { "char", "string" },
        { "text", "string" },
        { "tinyint", "byte" },
        { "smallint", "short" },
        { "int", "int" },
        { "bigint", "long" },
        { "float", "float" },
        { "double", "double" },
        { "decimal", "decimal" },
        { "date", "DateTime" },
        { "datetime", "DateTime" },
        { "timestamp", "DateTime" },
        { "bit", "bool" },
        { "tinyint(1)", "bool" },
        { "binary", "byte[]" },
        { "varbinary", "byte[]" },
        { "blob", "byte[]" },
        { "json", "string" },
        { "enum", "string" },
        { "set", "string" }
    };

    private readonly IHostEnvironment _environment;

    public CodeTemplateController(IHostEnvironment environment)
    {
        _connectionString = "Data Source=CodeTemplates.db";
        _environment = environment;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return; // Não inicializa o banco em produção
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Primeiro, vamos verificar se a coluna Namespace já existe
        var hasNamespaceColumn = connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM pragma_table_info('TableMetadata') WHERE name = 'Namespace'") > 0;

        if (!hasNamespaceColumn)
        {
            // Se a tabela já existe, adicionar a coluna Namespace
            try
            {
                connection.Execute("ALTER TABLE TableMetadata ADD COLUMN Namespace TEXT NOT NULL DEFAULT 'YourNamespace'");
            }
            catch
            {
                // Se der erro, provavelmente a tabela não existe ainda
            }
        }

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS ConnectionStrings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Value TEXT NOT NULL,
                Description TEXT
            )");
            
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS TableMetadata (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TableName TEXT NOT NULL UNIQUE,
                Namespace TEXT NOT NULL DEFAULT 'YourNamespace'
            )");
            
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS ColumnMetadata (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TableId INTEGER NOT NULL,
                ColumnName TEXT NOT NULL,
                DataType TEXT NOT NULL,
                IsNullable INTEGER NOT NULL,
                IsPrimaryKey INTEGER NOT NULL,
                FOREIGN KEY(TableId) REFERENCES TableMetadata(Id) ON DELETE CASCADE
            )");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Templates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Content TEXT NOT NULL,
                Description TEXT,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )");

        // Verificar se a coluna Namespace existe
        var columns = connection.Query<string>("PRAGMA table_info(TableMetadata)");
        if (!columns.Contains("Namespace"))
        {
            try
            {
                connection.Execute("ALTER TABLE TableMetadata ADD COLUMN Namespace TEXT NOT NULL DEFAULT ''");
            }
            catch (Exception ex)
            {
                // Se a coluna já existir, ignora o erro
                if (!ex.Message.Contains("duplicate column name"))
                {
                    throw;
                }
            }
        }
    }

    public IActionResult Index()
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return NotFound("This page is only available in development mode.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var tables = connection.Query<TableMetadata>("SELECT * FROM TableMetadata ORDER BY Id DESC").ToList();
        
        foreach (var table in tables)
        {
            table.Columns = connection.Query<ColumnMetadata>(
                "SELECT * FROM ColumnMetadata WHERE TableId = @TableId",
                new { TableId = table.Id }).ToList();
        }
        
        return View(tables);
    }

    public IActionResult Create()
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return NotFound("This page is only available in development mode.");
        }

        ViewBag.CSharpTypes = _csharpTypes;
        return View();
    }

    [HttpPost]
    public IActionResult Create(TableMetadata table)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return NotFound("This page is only available in development mode.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.CSharpTypes = _csharpTypes;
            return View(table);
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var tableId = connection.ExecuteScalar<int>(
                "INSERT INTO TableMetadata (TableName, Namespace) VALUES (@TableName, @Namespace); SELECT last_insert_rowid();",
                table,
                transaction);

            foreach (var column in table.Columns)
            {
                column.TableId = tableId;
                connection.Execute(
                    "INSERT INTO ColumnMetadata (TableId, ColumnName, DataType, IsNullable, IsPrimaryKey) " +
                    "VALUES (@TableId, @ColumnName, @DataType, @IsNullable, @IsPrimaryKey)",
                    column,
                    transaction);
            }

            transaction.Commit();
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IActionResult Edit(int id)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return NotFound("This page is only available in development mode.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var table = connection.QueryFirstOrDefault<TableMetadata>(
            "SELECT * FROM TableMetadata WHERE Id = @Id",
            new { Id = id });

        if (table == null)
        {
            return NotFound();
        }

        table.Columns = connection.Query<ColumnMetadata>(
            "SELECT * FROM ColumnMetadata WHERE TableId = @TableId",
            new { TableId = id }).ToList();

        ViewBag.CSharpTypes = _csharpTypes;
        return View(table);
    }

    [HttpPost]
    public IActionResult Edit(int id, TableMetadata table)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return NotFound("This page is only available in development mode.");
        }

        if (id != table.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.CSharpTypes = _csharpTypes;
            return View(table);
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // Atualiza a tabela
            connection.Execute(
                "UPDATE TableMetadata SET TableName = @TableName, Namespace = @Namespace WHERE Id = @Id",
                new { table.Id, table.TableName, table.Namespace },
                transaction);

            // Obtém os IDs das colunas existentes
            var existingColumnIds = connection.Query<int>(
                "SELECT Id FROM ColumnMetadata WHERE TableId = @TableId",
                new { TableId = id },
                transaction).ToList();

            // Obtém os IDs das colunas enviadas no formulário
            var submittedColumnIds = table.Columns
                .Where(c => c.Id > 0)
                .Select(c => c.Id)
                .ToList();

            // Remove colunas que não estão mais no formulário
            var columnsToDelete = existingColumnIds.Except(submittedColumnIds);
            foreach (var columnId in columnsToDelete)
            {
                connection.Execute(
                    "DELETE FROM ColumnMetadata WHERE Id = @Id",
                    new { Id = columnId },
                    transaction);
            }

            // Atualiza colunas existentes e adiciona novas
            foreach (var column in table.Columns)
            {
                if (column.Id > 0)
                {
                    // Atualiza coluna existente
                    connection.Execute(
                        "UPDATE ColumnMetadata SET ColumnName = @ColumnName, DataType = @DataType, IsNullable = @IsNullable, IsPrimaryKey = @IsPrimaryKey WHERE Id = @Id",
                        column,
                        transaction);
                }
                else
                {
                    // Adiciona nova coluna
                    column.TableId = id;
                    connection.Execute(
                        "INSERT INTO ColumnMetadata (TableId, ColumnName, DataType, IsNullable, IsPrimaryKey) " +
                        "VALUES (@TableId, @ColumnName, @DataType, @IsNullable, @IsPrimaryKey)",
                        column,
                        transaction);
                }
            }

            transaction.Commit();
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            ModelState.AddModelError("", "Error saving changes: " + ex.Message);
            ViewBag.CSharpTypes = _csharpTypes;
            return View(table);
        }
    }
    
    [HttpGet]
    public IActionResult GetCSharpType(string mySqlType)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        // Se o tipo MySQL estiver no dicionário, retorna o tipo C# correspondente
        if (_typeMappings.TryGetValue(mySqlType.ToLower(), out string csharpType))
        {
            return Json(new { success = true, csharpType = csharpType });
        }
        
        // Caso contrário, retorna string como padrão
        return Json(new { success = true, csharpType = "string" });
    }

    #region Connection Strings

    [HttpGet]
    public IActionResult GetConnectionStrings()
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var connectionStrings = connection.Query<ConnectionString>("SELECT * FROM ConnectionStrings ORDER BY Name").ToList();
        return Json(new { success = true, data = connectionStrings });
    }

    [HttpPost]
    public IActionResult SaveConnectionString([FromForm] ConnectionString connectionString)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Dados inválidos" });
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        try
        {
            if (connectionString.Id == 0)
            {
                // Inserir novo
                connection.Execute(
                    "INSERT INTO ConnectionStrings (Name, Value, Description) VALUES (@Name, @Value, @Description)",
                    connectionString);
            }
            else
            {
                // Atualizar existente
                connection.Execute(
                    "UPDATE ConnectionStrings SET Name = @Name, Value = @Value, Description = @Description WHERE Id = @Id",
                    connectionString);
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult DeleteConnectionString(int id)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("DELETE FROM ConnectionStrings WHERE Id = @Id", new { Id = id });
        return Json(new { success = true });
    }

    #endregion

    #region Database Tables

    [HttpGet]
    public async Task<IActionResult> GetDatabaseTables()
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        try
        {
            // Obter a connection string
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var connectionString = connection.QueryFirstOrDefault<ConnectionString>(
                "SELECT * FROM ConnectionStrings ORDER BY Id DESC LIMIT 1");

            if (connectionString == null)
            {
                return Json(new { success = false, message = "Connection string não encontrada" });
            }

            // Obter as tabelas do MySQL
            var tables = await GetMySqlTables(connectionString.Value);
            
            return Json(new { success = true, data = tables });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter tabelas: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    private string DetermineDatabaseType(string connectionString)
    {
        return "mysql";
    }

    private async Task<List<string>> GetMySqlTables(string connectionString)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var tables = await connection.QueryAsync<string>(
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE()");
                
            return tables.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter tabelas MySQL: {ex.Message}");
            return new List<string>();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetTableColumns(string tableName)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        try
        {
            // Obter a connection string
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var connectionString = connection.QueryFirstOrDefault<ConnectionString>(
                "SELECT * FROM ConnectionStrings ORDER BY Id DESC LIMIT 1");

            if (connectionString == null)
            {
                return Json(new { success = false, message = "Connection string não encontrada" });
            }

            // Obter as colunas do MySQL
            var columns = await GetMySqlTableColumns(connectionString.Value, tableName);
            return Json(new { success = true, data = columns });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter colunas: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    private async Task<List<TableColumn>> GetMySqlTableColumns(string connectionString, string tableName)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var columns = await connection.QueryAsync<TableColumn>(@"
                SELECT 
                    COLUMN_NAME as Name,
                    DATA_TYPE as DataType,
                    CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as IsNullable,
                    CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END as IsPrimaryKey
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @TableName
                AND TABLE_SCHEMA = DATABASE()
                ORDER BY ORDINAL_POSITION",
                new { TableName = tableName });
                
            return columns.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter colunas MySQL: {ex.Message}");
            return new List<TableColumn>();
        }
    }

    #endregion

    #region Save Table

    [HttpPost]
    public IActionResult SaveTable([FromBody] SaveTableRequest request)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // Verificar se a tabela já existe nos metadados
                var tableExists = connection.QueryFirstOrDefault<int>(
                    "SELECT COUNT(*) FROM TableMetadata WHERE TableName = @TableName",
                    new { TableName = request.TableName },
                    transaction) > 0;
                
                if (tableExists)
                {
                    transaction.Rollback();
                    return Json(new { 
                        success = false, 
                        message = $"Table '{request.TableName}' already exists in metadata" 
                    });
                }
                
                // Salvar metadados da tabela
                var tableId = connection.ExecuteScalar<int>(
                    "INSERT INTO TableMetadata (TableName) VALUES (@TableName); SELECT last_insert_rowid();",
                    new { TableName = request.TableName },
                    transaction);
                
                // Salvar metadados das colunas
                foreach (var column in request.Columns)
                {
                    connection.Execute(
                        "INSERT INTO ColumnMetadata (TableId, ColumnName, DataType, IsNullable, IsPrimaryKey) " +
                        "VALUES (@TableId, @ColumnName, @DataType, @IsNullable, @IsPrimaryKey)",
                        new
                        {
                            TableId = tableId,
                            ColumnName = column.Name,
                            DataType = column.DataType,
                            IsNullable = column.IsNullable,
                            IsPrimaryKey = column.IsPrimaryKey
                        },
                        transaction);
                }
                
                transaction.Commit();
                return Json(new { success = true });
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    
    private string GetSqliteDataType(string dataType)
    {
        // Mapeamento de tipos de dados para SQLite
        dataType = dataType.ToLower();
        
        if (dataType.Contains("char") || dataType.Contains("text") || dataType.Contains("varchar"))
            return "TEXT";
        else if (dataType.Contains("int"))
            return "INTEGER";
        else if (dataType.Contains("decimal") || dataType.Contains("numeric") || dataType.Contains("float") || dataType.Contains("double"))
            return "REAL";
        else if (dataType.Contains("date") || dataType.Contains("time"))
            return "TEXT";
        else if (dataType.Contains("bool") || dataType.Contains("bit"))
            return "INTEGER";
        else
            return "TEXT"; // Tipo padrão
    }
    
    public class SaveTableRequest
    {
        public string TableName { get; set; }
        public List<TableColumn> Columns { get; set; }
    }

    #endregion

    [HttpPost]
    public IActionResult GenerateModel(int id)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var table = connection.QueryFirstOrDefault<TableMetadata>(
                "SELECT * FROM TableMetadata WHERE Id = @Id",
                new { Id = id });

            if (table == null)
            {
                return Json(new { success = false, message = "Table not found" });
            }

            var columns = connection.Query<ColumnMetadata>(
                "SELECT * FROM ColumnMetadata WHERE TableId = @TableId",
                new { TableId = id }).ToList();

            // Criar diretório Models se não existir
            var modelsPath = Path.Combine(Directory.GetCurrentDirectory(), "Models");
            if (!Directory.Exists(modelsPath))
            {
                Directory.CreateDirectory(modelsPath);
            }

            // Converter o nome da tabela para PascalCase
            var fileName = ToPascalCase(table.TableName);
            var filePath = Path.Combine(modelsPath, $"{fileName}.cs");

            // Verificar se o arquivo já existe
            if (System.IO.File.Exists(filePath))
            {
                return Json(new { 
                    success = false, 
                    message = "File already exists", 
                    filePath = filePath 
                });
            }

            // Gerar o código do modelo
            var modelCode = GenerateModelCode(table, columns);
            System.IO.File.WriteAllText(filePath, modelCode);

            return Json(new { success = true, message = "Model generated successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove caracteres especiais e espaços
        var words = input.Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Converte cada palavra para PascalCase
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }

        return string.Concat(words);
    }

    private string GenerateModelCode(TableMetadata table, List<ColumnMetadata> columns)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("//dotnet aspnet-codegenerator view Create Create -m " + table.TableName + " -outDir Views/" + table.TableName);
        sb.AppendLine("//dotnet aspnet-codegenerator view Index List -m " + table.TableName + " -outDir Views/" + table.TableName);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine();
        sb.AppendLine($"namespace {table.Namespace}.Models");
        sb.AppendLine("{");
        sb.AppendLine($"    public class {ToPascalCase(table.TableName)}");
        sb.AppendLine("    {");

        foreach (var column in columns)
        {
            // Adicionar atributos de validação se necessário
            if (!column.IsNullable)
            {
                sb.AppendLine("        [Required]");
            }
            if (column.IsPrimaryKey)
            {
                sb.AppendLine("        [Key]");
            }

            // Usar o tipo diretamente do banco de dados
            var nullable = column.IsNullable ? "?" : "";
            sb.AppendLine($"        public {column.DataType}{nullable} {column.ColumnName} {{ get; set; }}");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    [HttpPost]
    public IActionResult GenerateRepository(int id)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var table = connection.QueryFirstOrDefault<TableMetadata>(
                "SELECT * FROM TableMetadata WHERE Id = @Id",
                new { Id = id });

            if (table == null)
            {
                return Json(new { success = false, message = "Table not found" });
            }

            // Verificar se o arquivo do modelo existe
            var fileName = ToPascalCase(table.TableName);
            var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "Models", $"{fileName}.cs");
            
            if (!System.IO.File.Exists(modelPath))
            {
                return Json(new { 
                    success = false, 
                    message = "Model file does not exist. Please generate the model first." 
                });
            }

            // Buscar o template DapperClean
            var template = connection.QueryFirstOrDefault<Template>(
                "SELECT * FROM Templates WHERE Name = 'DapperClean'");

            if (template == null)
            {
                return Json(new { success = false, message = "Template DapperClean not found" });
            }

            // Criar diretório Repositories se não existir
            var repositoriesPath = Path.Combine(Directory.GetCurrentDirectory(), "Repositories");
            if (!Directory.Exists(repositoriesPath))
            {
                Directory.CreateDirectory(repositoriesPath);
            }

            var filePath = Path.Combine(repositoriesPath, $"{fileName}Repository.cs");

            // Verificar se o arquivo já existe
            if (System.IO.File.Exists(filePath))
            {
                return Json(new { 
                    success = false, 
                    message = "File already exists", 
                    filePath = filePath 
                });
            }

            // Substituir @@Class pelo nome da classe em PascalCase
            var repositoryCode = template.Content.Replace("@@Class", fileName);
            System.IO.File.WriteAllText(filePath, repositoryCode);

            return Json(new { success = true, message = "Repository generated successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        // Verificar se estamos em ambiente de desenvolvimento
        if (!_environment.IsDevelopment())
        {
            return Json(new { success = false, message = "This endpoint is only available in development mode." });
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            // Verificar se a tabela existe
            var table = connection.QueryFirstOrDefault<TableMetadata>(
                "SELECT * FROM TableMetadata WHERE Id = @Id",
                new { Id = id });

            if (table == null)
            {
                return Json(new { success = false, message = "Table not found" });
            }

            // Excluir a tabela (as colunas serão excluídas automaticamente devido ao ON DELETE CASCADE)
            connection.Execute(
                "DELETE FROM TableMetadata WHERE Id = @Id",
                new { Id = id });

            return Json(new { success = true, message = "Table deleted successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}



//MODELS

public class CodeTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;

    // Propriedade de navegação
    public ICollection<TemplateField> Fields { get; set; } = new List<TemplateField>();
}

public class ColumnMetadata
{
    public int Id { get; set; }

    public int TableId { get; set; }

    [Required]
    public string ColumnName { get; set; }

    [Required]
    public string DataType { get; set; }

    public bool IsNullable { get; set; }

    public bool IsPrimaryKey { get; set; }
}
public class ConnectionString
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Nome")]
    public string Name { get; set; }

    [Required]
    [Display(Name = "Connection String")]
    public string Value { get; set; }

    [Display(Name = "Descrição")]
    public string Description { get; set; }
}

public class TableColumn
{
    [Display(Name = "Nome")]
    public string Name { get; set; }

    [Display(Name = "Tipo de Dados")]
    public string DataType { get; set; }

    [Display(Name = "Permite Nulo")]
    public bool IsNullable { get; set; }

    [Display(Name = "Chave Primária")]
    public bool IsPrimaryKey { get; set; }
}

public class TableMetadata
{
    public int Id { get; set; }

    [Required]
    public string TableName { get; set; }

    [Required]
    public string Namespace { get; set; }

    public List<ColumnMetadata> Columns { get; set; } = new List<ColumnMetadata>();
}

public class Template
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TemplateField
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }

    // Propriedade de navegação
    public CodeTemplate? Template { get; set; }
}