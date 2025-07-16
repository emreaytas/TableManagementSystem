using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using TableManagement.Core.Entities;
using TableManagement.Core.Enums;

namespace TableManagement.Application.Services
{
    public class DataDefinitionService : IDataDefinitionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataDefinitionService> _logger;
        private readonly string _connectionString;

        // Güvenli tablo adı pattern'i
        private readonly Regex _safeNamePattern = new Regex(@"^[a-zA-Z][a-zA-Z0-9_]*$");

        public DataDefinitionService(IConfiguration configuration, ILogger<DataDefinitionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        public async Task<bool> CreateUserTableAsync(string tableName, List<CustomColumn> columns, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                // Tablo adının güvenli olup olmadığını kontrol et
                if (!IsSecureTableName(secureTableName))
                {
                    _logger.LogWarning("Invalid table name: {TableName}", secureTableName);
                    return false;
                }

                var ddlCommand = BuildCreateTableCommand(secureTableName, columns);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(ddlCommand, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("User table created successfully: {TableName} for user {UserId}",
                    secureTableName, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user table: {TableName} for user {UserId}",
                    tableName, userId);
                return false;
            }
        }

        public async Task<bool> DropUserTableAsync(string tableName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                if (!IsSecureTableName(secureTableName))
                {
                    _logger.LogWarning("Invalid table name: {TableName}", secureTableName);
                    return false;
                }

                var ddlCommand = $"DROP TABLE IF EXISTS [{secureTableName}]";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(ddlCommand, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("User table dropped successfully: {TableName} for user {UserId}",
                    secureTableName, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dropping user table: {TableName} for user {UserId}",
                    tableName, userId);
                return false;
            }
        }

        public async Task<bool> InsertDataToUserTableAsync(string tableName, Dictionary<string, object> data, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                if (!IsSecureTableName(secureTableName))
                {
                    _logger.LogWarning("Invalid table name: {TableName}", secureTableName);
                    return false;
                }

                var columns = string.Join(", ", data.Keys.Select(k => $"[{SanitizeColumnName(k)}]"));
                var parameters = string.Join(", ", data.Keys.Select(k => $"@{SanitizeColumnName(k)}"));

                var insertCommand = $"INSERT INTO [{secureTableName}] ({columns}) VALUES ({parameters})";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(insertCommand, connection);

                foreach (var kvp in data)
                {
                    var parameterName = $"@{SanitizeColumnName(kvp.Key)}";
                    command.Parameters.AddWithValue(parameterName, kvp.Value ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Data inserted successfully to table: {TableName} for user {UserId}",
                    secureTableName, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting data to table: {TableName} for user {UserId}",
                    tableName, userId);
                return false;
            }
        }

        public async Task<List<Dictionary<string, object>>> SelectDataFromUserTableAsync(string tableName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                if (!IsSecureTableName(secureTableName))
                {
                    _logger.LogWarning("Invalid table name: {TableName}", secureTableName);
                    return new List<Dictionary<string, object>>();
                }

                var selectCommand = $"SELECT * FROM [{secureTableName}] ORDER BY Id";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(selectCommand, connection);
                using var reader = await command.ExecuteReaderAsync();

                var results = new List<Dictionary<string, object>>();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var fieldName = reader.GetName(i);
                        var fieldValue = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[fieldName] = fieldValue;
                    }
                    results.Add(row);
                }

                _logger.LogInformation("Data selected successfully from table: {TableName} for user {UserId}",
                    secureTableName, userId);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting data from table: {TableName} for user {UserId}",
                    tableName, userId);
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<bool> UpdateDataInUserTableAsync(string tableName, Dictionary<string, object> data, string whereClause, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                if (!IsSecureTableName(secureTableName))
                {
                    _logger.LogWarning("Invalid table name: {TableName}", secureTableName);
                    return false;
                }

                var setClause = string.Join(", ", data.Keys.Select(k => $"[{SanitizeColumnName(k)}] = @{SanitizeColumnName(k)}"));
                var updateCommand = $"UPDATE [{secureTableName}] SET {setClause} WHERE {whereClause}";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(updateCommand, connection);

                foreach (var kvp in data)
                {
                    var parameterName = $"@{SanitizeColumnName(kvp.Key)}";
                    command.Parameters.AddWithValue(parameterName, kvp.Value ?? DBNull.Value);
                }

                var affectedRows = await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Data updated successfully in table: {TableName} for user {UserId}. Affected rows: {AffectedRows}",
                    secureTableName, userId, affectedRows);

                return affectedRows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data in table: {TableName} for user {UserId}",
                    tableName, userId);
                return false;
            }
        }

        public async Task<bool> DeleteDataFromUserTableAsync(string tableName, string whereClause, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                if (!IsSecureTableName(secureTableName))
                {
                    _logger.LogWarning("Invalid table name: {TableName}", secureTableName);
                    return false;
                }

                var deleteCommand = $"DELETE FROM [{secureTableName}] WHERE {whereClause}";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(deleteCommand, connection);
                var affectedRows = await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Data deleted successfully from table: {TableName} for user {UserId}. Affected rows: {AffectedRows}",
                    secureTableName, userId, affectedRows);

                return affectedRows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data from table: {TableName} for user {UserId}",
                    tableName, userId);
                return false;
            }
        }

        public string GenerateSecureTableName(string tableName, int userId)
        {
            // Kullanıcı ID'si ile prefix ekleyerek güvenli tablo adı oluştur
            var sanitizedTableName = SanitizeTableName(tableName);
            return $"UserTable_{userId}_{sanitizedTableName}";
        }

        public string ConvertToSqlDataType(ColumnDataType dataType)
        {
            return dataType switch
            {
                ColumnDataType.Varchar => "NVARCHAR(255)",
                ColumnDataType.Int => "INT",
                ColumnDataType.Decimal => "DECIMAL(18,2)",
                ColumnDataType.DateTime => "DATETIME2",
                _ => "NVARCHAR(255)"
            };
        }

        private string BuildCreateTableCommand(string tableName, List<CustomColumn> columns)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{tableName}] (");
            sb.AppendLine("    [Id] INT IDENTITY(1,1) PRIMARY KEY,");
            sb.AppendLine("    [CreatedAt] DATETIME2 DEFAULT GETUTCDATE(),");
            sb.AppendLine("    [UpdatedAt] DATETIME2 NULL,");

            foreach (var column in columns.OrderBy(c => c.DisplayOrder))
            {
                var sqlDataType = ConvertToSqlDataType(column.DataType);
                var nullable = column.IsRequired ? "NOT NULL" : "NULL";
                var defaultValue = GetDefaultValueClause(column);

                sb.AppendLine($"    [{SanitizeColumnName(column.ColumnName)}] {sqlDataType} {nullable}{defaultValue},");
            }

            // Son virgülü kaldır
            var commandText = sb.ToString().TrimEnd(',', '\r', '\n');
            commandText += Environment.NewLine + ")";

            return commandText;
        }

        private string GetDefaultValueClause(CustomColumn column)
        {
            if (string.IsNullOrEmpty(column.DefaultValue))
                return string.Empty;

            return column.DataType switch
            {
                ColumnDataType.Varchar => $" DEFAULT '{column.DefaultValue.Replace("'", "''")}'",
                ColumnDataType.Int => int.TryParse(column.DefaultValue, out _) ? $" DEFAULT {column.DefaultValue}" : string.Empty,
                ColumnDataType.Decimal => decimal.TryParse(column.DefaultValue, out _) ? $" DEFAULT {column.DefaultValue}" : string.Empty,
                ColumnDataType.DateTime => $" DEFAULT '{column.DefaultValue}'",
                _ => string.Empty
            };
        }

        private bool IsSecureTableName(string tableName)
        {
            return _safeNamePattern.IsMatch(tableName) && tableName.Length <= 128;
        }

        private string SanitizeTableName(string tableName)
        {
            // Sadece harf, rakam ve alt çizgi karakterlerini korur
            var sanitized = Regex.Replace(tableName, @"[^a-zA-Z0-9_]", "_");

            // Rakam ile başlarsa önüne T ekle
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "T" + sanitized;
            }

            // Maksimum 50 karakter
            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 50);
            }

            return sanitized;
        }

        private string SanitizeColumnName(string columnName)
        {
            // Sadece harf, rakam ve alt çizgi karakterlerini korur
            var sanitized = Regex.Replace(columnName, @"[^a-zA-Z0-9_]", "_");

            // Rakam ile başlarsa önüne C ekle
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "C" + sanitized;
            }

            // Maksimum 50 karakter
            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 50);
            }

            return sanitized;
        }
    }
}