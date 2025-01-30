using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace BaseDapper
{
    public class DbContext
    {
        IDbConnection _connection;
        public DbContext(IDbConnection connection)
        {
            _connection = connection;
        }

        public IDbConnection GetConnection()
        {
            return _connection;
        }
        public async Task<IEnumerable<T>> QueryAsync<T>(object param)
        {
            string sql = $"SELECT * FROM {GetTableName<T>()}";
            return await _connection.QueryAsync<T>(sql, param);
        }
        public async Task<IEnumerable<T>> QueryAsync<T>(string expressao, object param)
        {
            string sql = $"SELECT * FROM {GetTableName<T>()} WHERE {expressao}";
            return await _connection.QueryAsync<T>(sql, param);
        }
        public async Task<T> QueryFirstOrDefaultAsync<T>(object param)
        {
            string sql = $"SELECT * FROM {GetTableName<T>()} WHERE Id = @Id";
            return await _connection.QueryFirstOrDefaultAsync<T>(sql, param);
        }

        public async Task<int> InsertAsync<T>(T entity, string ignore = "Id")
        {
            ValidarObjeto(entity);
            string sql = GenSqlInsert<T>(ignore);
            return await _connection.ExecuteAsync(sql, entity);
        }
        public async Task<T> GetLastInsertedAsync<T>(string idColumn = "Id")
        {
            string sql = $@"
                SELECT * FROM {GetTableName<T>()} 
                WHERE {idColumn} = (SELECT MAX({idColumn}) FROM {GetTableName<T>()})";

            return await _connection.QueryFirstOrDefaultAsync<T>(sql);
        }

        public async Task<int> UpdateAsync<T>(T entity, string ignore = "Id")
        {
            ValidarObjeto(entity);
            string sql = GenSqlUpdate<T>(ignore);
            return await _connection.ExecuteAsync(sql, entity);
        }

        public async Task<int> Delete<T>(object param)
        {
            string sql = $"DELETE FROM {GetTableName<T>()} WHERE Id = @Id";
            return await _connection.ExecuteAsync(sql, param);
        }
        private string GetTableName<T>()
        {
            var atr = typeof(T).GetCustomAttribute<TableAttribute>();
            return atr == null ? typeof(T).Name : atr.Name;
        }

        private string GetColumns<T>()
        {
            var columns = typeof(T).GetProperties().Select(p => p.Name);
            return string.Join(",", columns);
        }

        private string GenSqlInsert<T>(string ignore)
        {
            string[] ignoreColumns = ignore.Split(',');
            var tableName = GetTableName<T>();
            var columns = GetColumns<T>();
            var columnsInsert = string.Join(",", columns.Split(',').Where(c => !ignoreColumns.Contains(c)));
            return $"INSERT INTO {tableName} ({columnsInsert}) VALUES ({string.Join(",", columnsInsert.Split(',').Select(c => $"@{c}"))})";
        }

        private string GenSqlUpdate<T>(string ignore)
        {
            string[] ignoreColumns = ignore.Split(',');
            var tableName = GetTableName<T>();
            var columns = GetColumns<T>();
            var columnsUpdate = string.Join(",", columns.Split(',').Where(c => !ignoreColumns.Contains(c)).Select(c => $"{c} = @{c}"));
            return $"UPDATE {tableName} SET {columnsUpdate} WHERE Id = @Id";
        }

        public async Task PrintColumnsTypeSQLSERVER(string tableName)
        {
            string sql = @"SELECT 
                        c.name AS NomeDaColuna,
                        t.name AS TipoDeDado,
                        c.max_length AS TamanhoMaximo
                        FROM sys.columns c
                        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                        WHERE c.object_id = OBJECT_ID('" + tableName + "') ORDER BY c.column_id;";
            var result = await _connection.QueryAsync(sql);

            foreach (var item in result)
            {
                Console.WriteLine($"public {parseType(item.TipoDeDado)} {item.NomeDaColuna} {{get;set;}}");
            }
        }

        public async Task PrintColumnsTypeMySQL(string tableName)
        {
            string sql = @"SELECT 
                        COLUMN_NAME AS NomeDaColuna,
                        DATA_TYPE AS TipoDeDado
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + tableName + "'ORDER BY ORDINAL_POSITION;";
            var result = await _connection.QueryAsync(sql);

            foreach (var item in result)
            {
                Console.WriteLine($"public {parseType(item.TipoDeDado)} {item.NomeDaColuna} {{get;set;}}");
            }
        }

        public async Task PrintColumnsTypeSQLite(string tableName)
        {
            string sql = @"PRAGMA table_info('" + tableName + ");";
            var result = await _connection.QueryAsync(sql);

            foreach (var item in result)
            {
                Console.WriteLine($"public {parseType(item.type)} {item.name} {{get;set;}}");
            }
        }
        private string parseType(string type)
        {
            switch (type)
            {
                case "int":
                    return "int";
                case "varchar":
                    return "string ?";
                case "datetime":
                    return "DateTime";
                case "bit":
                    return "bool";
                default:
                    return "string";
            }
        }

        private void ValidarObjeto(object obj)
        {
            var resultados = new List<ValidationResult>();
            var contexto = new ValidationContext(obj, serviceProvider: null, items: null);

            Validator.TryValidateObject(obj, contexto, resultados, validateAllProperties: true);

            StringBuilder stringBuilder = new StringBuilder("Campos Inválidos: ");
            if (resultados.Count() > 0)
            {
                foreach (var item in resultados)
                    stringBuilder.AppendLine(item.ErrorMessage);


                throw new Exception(stringBuilder.ToString());
            }
        }

    }

    //------------------------------------------------------------------------------
    //BASE REPOSOITORY
    //------------------------------------------------------------------------------
    public class BaseRespository<T>
    {
        private readonly DbContext _db;
        public BaseRespository(DbContext db)
        {
            _db = db;
        }
        public async Task<IEnumerable<T>> GetAll()
        {
            return await _db.QueryAsync<T>(new { });
        }
        
        public async Task<IEnumerable<T>> GetWhere(string expressao, object param)
        {
            return await _db.QueryAsync<T>(expressao, param);
        }
        public async Task<T> GetOne(int id)
        {
            return await _db.QueryFirstOrDefaultAsync<T>(new { Id = id });
        }
        public async Task<T> GetLast(string idColumn = "Id")
        {
            return await _db.GetLastInsertedAsync<T>(idColumn);
        }
        public async Task<int> Insert(T model, string ignore = "Id")
        {
            return await _db.InsertAsync<T>(model, ignore);
        }
        public async Task<int> Update(T model, string ignore = "Id")
        {
            return await _db.UpdateAsync<T>(model, ignore);
        }
        public async Task<int> Delete(int id)
        {
            return await _db.Delete<T>(new { Id = id });
        }
    }

}
