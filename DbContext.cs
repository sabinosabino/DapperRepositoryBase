using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
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
            string sql = GenSqlInsert<T>(ignore);
            return await _connection.ExecuteAsync(sql, entity);
        }

        public async Task<int> UpdateAsync<T>(T entity, string ignore = "Id")
        {
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
        public async Task<int> Insert(T unidade)
        {
            return await _db.InsertAsync<T>(unidade);
        }
        public async Task<int> Update(T unidade)
        {
            return await _db.UpdateAsync<T>(unidade);
        }
        public async Task<int> Delete(int id)
        {
            return await _db.Delete<T>(new { Id = id });
        }
    }

}
