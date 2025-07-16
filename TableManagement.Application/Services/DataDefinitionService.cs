using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using TableManagement.Core.Entities;
using TableManagement.Core.Enums;
using TableManagement.Application.DTOs.Responses;

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

        // ========== KOLON GÜNCELLEME METODLARI ==========

        public async Task<ColumnUpdateResult> UpdateColumnDataTypeAsync(string tableName, string columnName, ColumnDataType newDataType, bool forceUpdate, int userId)
        {
            var result = new ColumnUpdateResult();

            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                if (!IsSecureTableName(secureTableName))
                {
                    result.Message = "Geçersiz tablo adı";
                    return result;
                }

                // Önce validation yap
                var validationResult = await ValidateColumnUpdateAsync(tableName, columnName, newDataType, userId);
                result.ValidationResult = validationResult;

                if (!validationResult.IsValid)
                {
                    result.Message = "Kolon güncellenemez: " + string.Join(", ", validationResult.Issues);
                    return result;
                }

                if (validationResult.HasDataCompatibilityIssues && !forceUpdate)
                {
                    result.Message = "Veri uyumsuzluğu tespit edildi. ForceUpdate=true ile tekrar deneyin.";
                    result.Success = false;
                    return result;
                }

                // ALTER TABLE komutu oluştur
                var newSqlDataType = ConvertToSqlDataType(newDataType);
                var alterCommand = $"ALTER TABLE [{secureTableName}] ALTER COLUMN [{sanitizedColumnName}] {newSqlDataType}";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Backup işlemi (kritik durumlarda)
                    if (validationResult.HasDataCompatibilityIssues && forceUpdate)
                    {
                        await HandleDataCompatibilityIssuesAsync(connection, transaction, secureTableName, sanitizedColumnName, newDataType, validationResult.DataIssues);
                    }

                    // ALTER TABLE komutunu çalıştır
                    using var command = new SqlCommand(alterCommand, connection, transaction);
                    await command.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();

                    result.Success = true;
                    result.Message = "Kolon başarıyla güncellendi";
                    result.ExecutedQueries.Add(alterCommand);

                    _logger.LogInformation("Column data type updated successfully: {TableName}.{ColumnName} to {NewDataType} for user {UserId}",
                        secureTableName, sanitizedColumnName, newDataType, userId);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column data type: {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
                result.Message = "Kolon güncellenirken hata oluştu: " + ex.Message;
            }

            return result;
        }

        public async Task<ColumnValidationResult> ValidateColumnUpdateAsync(string tableName, string columnName, ColumnDataType newDataType, int userId)
        {
            var result = new ColumnValidationResult { IsValid = true };

            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Mevcut kolon bilgilerini al
                var currentColumnInfo = await GetColumnInfoAsync(connection, secureTableName, sanitizedColumnName);
                if (currentColumnInfo == null)
                {
                    result.IsValid = false;
                    result.Issues.Add("Kolon bulunamadı");
                    return result;
                }

                var currentDataType = ParseSqlDataTypeToEnum(currentColumnInfo.DataType);

                // Aynı tip kontrolü
                if (currentDataType == newDataType)
                {
                    result.Issues.Add("Kolon zaten bu veri tipinde");
                    return result;
                }

                // Dönüşüm mümkün mü kontrol et
                if (!CanConvertTo(currentDataType, newDataType))
                {
                    result.IsValid = false;
                    result.Issues.Add($"{currentDataType} tipinden {newDataType} tipine dönüşüm desteklenmiyor");
                    return result;
                }

                // Veri uyumluluğunu kontrol et
                await ValidateDataCompatibilityAsync(connection, secureTableName, sanitizedColumnName, currentDataType, newDataType, result);

                // Kayıplı dönüşüm kontrolü
                if (IsLossyConversion(currentDataType, newDataType))
                {
                    result.HasDataCompatibilityIssues = true;
                    result.RequiresForceUpdate = true;
                    result.Issues.Add("Bu dönüşüm veri kaybına neden olabilir");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating column update: {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
                result.IsValid = false;
                result.Issues.Add("Validation sırasında hata oluştu: " + ex.Message);
            }

            return result;
        }

        public async Task<bool> RenameColumnAsync(string tableName, string oldColumnName, string newColumnName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedOldColumnName = SanitizeColumnName(oldColumnName);
                var sanitizedNewColumnName = SanitizeColumnName(newColumnName);

                if (!IsSecureTableName(secureTableName))
                    return false;

                var renameCommand = $"EXEC sp_rename '{secureTableName}.{sanitizedOldColumnName}', '{sanitizedNewColumnName}', 'COLUMN'";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(renameCommand, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Column renamed successfully: {TableName}.{OldColumnName} to {NewColumnName} for user {UserId}",
                    secureTableName, sanitizedOldColumnName, sanitizedNewColumnName, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming column: {TableName}.{OldColumnName} to {NewColumnName} for user {UserId}",
                    tableName, oldColumnName, newColumnName, userId);
                return false;
            }
        }

        public async Task<bool> UpdateColumnDefaultValueAsync(string tableName, string columnName, string defaultValue, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                if (!IsSecureTableName(secureTableName))
                    return false;

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Önce mevcut default constraint'i kaldır
                var dropDefaultCommand = $@"
                    DECLARE @ConstraintName NVARCHAR(200)
                    SELECT @ConstraintName = name 
                    FROM sys.default_constraints 
                    WHERE parent_object_id = object_id('{secureTableName}') 
                    AND parent_column_id = (SELECT column_id FROM sys.columns WHERE object_id = object_id('{secureTableName}') AND name = '{sanitizedColumnName}')
                    
                    IF @ConstraintName IS NOT NULL
                        EXEC('ALTER TABLE [{secureTableName}] DROP CONSTRAINT ' + @ConstraintName)";

                using var dropCommand = new SqlCommand(dropDefaultCommand, connection);
                await dropCommand.ExecuteNonQueryAsync();

                // Yeni default constraint ekle
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    var constraintName = $"DF_{secureTableName}_{sanitizedColumnName}";
                    var addDefaultCommand = $"ALTER TABLE [{secureTableName}] ADD CONSTRAINT [{constraintName}] DEFAULT '{defaultValue.Replace("'", "''")}' FOR [{sanitizedColumnName}]";

                    using var addCommand = new SqlCommand(addDefaultCommand, connection);
                    await addCommand.ExecuteNonQueryAsync();
                }

                _logger.LogInformation("Column default value updated successfully: {TableName}.{ColumnName} for user {UserId}",
                    secureTableName, sanitizedColumnName, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column default value: {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
                return false;
            }
        }

        public async Task<bool> UpdateColumnNullabilityAsync(string tableName, string columnName, bool isRequired, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                if (!IsSecureTableName(secureTableName))
                    return false;

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Mevcut kolon bilgilerini al
                var columnInfo = await GetColumnInfoAsync(connection, secureTableName, sanitizedColumnName);
                if (columnInfo == null)
                    return false;

                var nullability = isRequired ? "NOT NULL" : "NULL";
                var alterCommand = $"ALTER TABLE [{secureTableName}] ALTER COLUMN [{sanitizedColumnName}] {columnInfo.DataType} {nullability}";

                using var command = new SqlCommand(alterCommand, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Column nullability updated successfully: {TableName}.{ColumnName} to {IsRequired} for user {UserId}",
                    secureTableName, sanitizedColumnName, isRequired, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column nullability: {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
                return false;
            }
        }

        // ========== PRIVATE HELPER METODLARI ==========

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
            var sanitized = Regex.Replace(tableName, @"[^a-zA-Z0-9_]", "_");

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "T" + sanitized;
            }

            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 50);
            }

            return sanitized;
        }

        private string SanitizeColumnName(string columnName)
        {
            var sanitized = Regex.Replace(columnName, @"[^a-zA-Z0-9_]", "_");

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "C" + sanitized;
            }

            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 50);
            }

            return sanitized;
        }

        private async Task<ColumnInfo?> GetColumnInfoAsync(SqlConnection connection, string tableName, string columnName)
        {
            var query = $@"
                SELECT 
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = '{tableName}' AND c.COLUMN_NAME = '{columnName}'";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new ColumnInfo
                {
                    DataType = reader.GetString("DATA_TYPE"),
                    MaxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                    Precision = reader.IsDBNull("NUMERIC_PRECISION") ? null : reader.GetByte("NUMERIC_PRECISION"),
                    Scale = reader.IsDBNull("NUMERIC_SCALE") ? null : reader.GetInt32("NUMERIC_SCALE"),
                    IsNullable = reader.GetString("IS_NULLABLE") == "YES"
                };
            }

            return null;
        }

        private async Task ValidateDataCompatibilityAsync(SqlConnection connection, string tableName, string columnName,
            ColumnDataType currentType, ColumnDataType newType, ColumnValidationResult result)
        {
            var query = $"SELECT Id, [{columnName}] FROM [{tableName}] WHERE [{columnName}] IS NOT NULL";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var issues = new List<DataConversionIssue>();
            int rowCount = 0;

            while (await reader.ReadAsync())
            {
                rowCount++;
                var value = reader.GetValue(1)?.ToString() ?? "";
                var rowId = reader.GetInt32(0);

                if (!CanConvertValue(value, currentType, newType))
                {
                    issues.Add(new DataConversionIssue
                    {
                        RowId = rowId,
                        CurrentValue = value,
                        IssueDescription = $"'{value}' değeri {newType} tipine dönüştürülemiyor",
                        SuggestedAction = "Veriyi manuel olarak düzeltin veya ForceUpdate kullanın"
                    });
                }
            }

            result.AffectedRowCount = rowCount;
            result.DataIssues = issues;
            result.HasDataCompatibilityIssues = issues.Any();
        }

        private bool CanConvertValue(string value, ColumnDataType currentType, ColumnDataType newType)
        {
            if (string.IsNullOrEmpty(value)) return true;

            return newType switch
            {
                ColumnDataType.Int => int.TryParse(value, out _),
                ColumnDataType.Decimal => decimal.TryParse(value, out _),
                ColumnDataType.DateTime => DateTime.TryParse(value, out _),
                ColumnDataType.Varchar => true,
                _ => false
            };
        }

        private async Task HandleDataCompatibilityIssuesAsync(SqlConnection connection, SqlTransaction transaction,
            string tableName, string columnName, ColumnDataType newType, List<DataConversionIssue> issues)
        {
            foreach (var issue in issues)
            {
                var defaultValue = GetDefaultValueForType(newType);
                var updateCommand = $"UPDATE [{tableName}] SET [{columnName}] = {defaultValue} WHERE Id = {issue.RowId}";

                using var command = new SqlCommand(updateCommand, connection, transaction);
                await command.ExecuteNonQueryAsync();
            }
        }

        private string GetDefaultValueForType(ColumnDataType dataType)
        {
            return dataType switch
            {
                ColumnDataType.Int => "0",
                ColumnDataType.Decimal => "0.0",
                ColumnDataType.DateTime => "GETDATE()",
                ColumnDataType.Varchar => "''",
                _ => "NULL"
            };
        }

        private ColumnDataType ParseSqlDataTypeToEnum(string sqlDataType)
        {
            return sqlDataType.ToUpperInvariant() switch
            {
                "NVARCHAR" => ColumnDataType.Varchar,
                "VARCHAR" => ColumnDataType.Varchar,
                "INT" => ColumnDataType.Int,
                "DECIMAL" => ColumnDataType.Decimal,
                "DATETIME2" => ColumnDataType.DateTime,
                "DATETIME" => ColumnDataType.DateTime,
                _ => ColumnDataType.Varchar
            };
        }

        private bool CanConvertTo(ColumnDataType from, ColumnDataType to)
        {
            if (from == to) return true;

            return from switch
            {
                ColumnDataType.Varchar => true,
                ColumnDataType.Int => to == ColumnDataType.Decimal || to == ColumnDataType.Varchar,
                ColumnDataType.Decimal => to == ColumnDataType.Varchar,
                ColumnDataType.DateTime => to == ColumnDataType.Varchar,
                _ => false
            };
        }

        private bool IsLossyConversion(ColumnDataType from, ColumnDataType to)
        {
            return from switch
            {
                ColumnDataType.Decimal => to == ColumnDataType.Int,
                ColumnDataType.DateTime => to != ColumnDataType.Varchar,
                _ => false
            };
        }
    }

    // Helper sınıfı
    public class ColumnInfo
    {
        public string DataType { get; set; } = string.Empty;
        public int? MaxLength { get; set; }
        public byte? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
    }
}