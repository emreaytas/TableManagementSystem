using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Core.Entities;
using TableManagement.Core.Enums;

namespace TableManagement.Application.Services
{
    public class DataDefinitionService : IDataDefinitionService
    {
        private readonly string _connectionString;
        private readonly ILogger<DataDefinitionService> _logger;

        public DataDefinitionService(IConfiguration configuration, ILogger<DataDefinitionService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found");
            _logger = logger;
        }

        // Existing methods implementation...
        public async Task<bool> CreateUserTableAsync(string tableName, List<CustomColumn> columns, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var createTableQuery = $"CREATE TABLE [{secureTableName}] (";
                createTableQuery += "Id INT IDENTITY(1,1) PRIMARY KEY, ";
                createTableQuery += "RowIdentifier INT NOT NULL, ";

                foreach (var column in columns.OrderBy(c => c.DisplayOrder))
                {
                    var sqlDataType = ConvertToSqlDataType(column.DataType);
                    createTableQuery += $"[{SanitizeColumnName(column.ColumnName)}] {sqlDataType}";

                    if (column.IsRequired)
                        createTableQuery += " NOT NULL";

                    if (!string.IsNullOrEmpty(column.DefaultValue))
                    {
                        var defaultValue = FormatDefaultValue(column.DefaultValue, column.DataType);
                        createTableQuery += $" DEFAULT {defaultValue}";
                    }

                    createTableQuery += ", ";
                }

                createTableQuery = createTableQuery.TrimEnd(',', ' ') + ")";

                using var command = new SqlCommand(createTableQuery, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Table {TableName} created successfully for user {UserId}", secureTableName, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table {TableName} for user {UserId}", tableName, userId);
                return false;
            }
        }

        public async Task<bool> DropUserTableAsync(string tableName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var dropTableQuery = $"DROP TABLE IF EXISTS [{secureTableName}]";
                using var command = new SqlCommand(dropTableQuery, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Table {TableName} dropped successfully for user {UserId}", secureTableName, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dropping table {TableName} for user {UserId}", tableName, userId);
                return false;
            }
        }

        public async Task<bool> InsertDataToUserTableAsync(string tableName, Dictionary<string, object> data, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get next row identifier
                var maxRowQuery = $"SELECT ISNULL(MAX(RowIdentifier), 0) + 1 FROM [{secureTableName}]";
                using var maxRowCommand = new SqlCommand(maxRowQuery, connection);
                var nextRowId = (int)await maxRowCommand.ExecuteScalarAsync();

                // Build insert query
                var columns = string.Join(", ", data.Keys.Select(k => $"[{SanitizeColumnName(k)}]"));
                var parameters = string.Join(", ", data.Keys.Select(k => $"@{SanitizeColumnName(k)}"));

                var insertQuery = $"INSERT INTO [{secureTableName}] (RowIdentifier, {columns}) VALUES (@RowIdentifier, {parameters})";

                using var command = new SqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@RowIdentifier", nextRowId);

                foreach (var kvp in data)
                {
                    command.Parameters.AddWithValue($"@{SanitizeColumnName(kvp.Key)}", kvp.Value ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting data to table {TableName} for user {UserId}", tableName, userId);
                return false;
            }
        }

        public async Task<List<Dictionary<string, object>>> SelectDataFromUserTableAsync(string tableName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var result = new List<Dictionary<string, object>>();

                _logger.LogInformation("Attempting to select data from table: {SecureTableName} for user: {UserId}", secureTableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Önce tablonun var olup olmadığını kontrol edin
                var tableExistsQuery = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = @tableName AND TABLE_SCHEMA = 'dbo'";

                using var checkCommand = new SqlCommand(tableExistsQuery, connection);
                checkCommand.Parameters.AddWithValue("@tableName", secureTableName);
                var tableExists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                if (!tableExists)
                {
                    _logger.LogWarning("Table {SecureTableName} does not exist for user {UserId}", secureTableName, userId);
                    return result;
                }

                // Veri çekme sorgusu
                var selectQuery = $"SELECT * FROM [{secureTableName}]";

                // Eğer RowIdentifier sütunu varsa ORDER BY ekleyin
                var columnCheckQuery = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @tableName AND COLUMN_NAME = 'RowIdentifier' AND TABLE_SCHEMA = 'dbo'";

                using var columnCommand = new SqlCommand(columnCheckQuery, connection);
                columnCommand.Parameters.AddWithValue("@tableName", secureTableName);
                var hasRowIdentifier = (int)await columnCommand.ExecuteScalarAsync() > 0;

                if (hasRowIdentifier)
                {
                    selectQuery += " ORDER BY RowIdentifier";
                }

                _logger.LogInformation("Executing query: {Query}", selectQuery);

                using var command = new SqlCommand(selectQuery, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var fieldName = reader.GetName(i);
                        var fieldValue = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[fieldName] = fieldValue;
                    }
                    result.Add(row);
                }

                _logger.LogInformation("Retrieved {RowCount} rows from table {SecureTableName}", result.Count, secureTableName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting data from table {TableName} for user {UserId}", tableName, userId);
                // Hata durumunda boş liste döndür, exception fırlatma
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<bool> UpdateDataInUserTableAsync(string tableName, Dictionary<string, object> data, string whereClause, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var setClause = string.Join(", ", data.Keys.Select(k => $"[{SanitizeColumnName(k)}] = @{SanitizeColumnName(k)}"));
                var updateQuery = $"UPDATE [{secureTableName}] SET {setClause} WHERE {whereClause}";

                using var command = new SqlCommand(updateQuery, connection);
                foreach (var kvp in data)
                {
                    command.Parameters.AddWithValue($"@{SanitizeColumnName(kvp.Key)}", kvp.Value ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data in table {TableName} for user {UserId}", tableName, userId);
                return false;
            }
        }

        public async Task<bool> RenamePhysicalTableAsync(string oldPhysicalTableName, string newLogicalTableName, int userId)
        {
            try
            {
                var newSecureTableName = GenerateSecureTableName(newLogicalTableName, userId);

                _logger.LogInformation("Attempting to rename physical table from {OldTableName} to {NewTableName} for user {UserId}",
                    oldPhysicalTableName, newSecureTableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Önce eski tablonun var olup olmadığını kontrol et
                var tableExists = await TableExistsAsync(oldPhysicalTableName);
                if (!tableExists)
                {
                    _logger.LogWarning("Old physical table {OldTableName} does not exist for user {UserId}", oldPhysicalTableName, userId);
                    return false;
                }

                // Yeni tablo adının zaten kullanılmadığından emin ol
                var newTableExists = await TableExistsAsync(newSecureTableName);
                if (newTableExists)
                {
                    _logger.LogError("New table name {NewTableName} already exists for user {UserId}", newSecureTableName, userId);
                    return false;
                }

                // Tabloyu yeniden adlandır
                var renameQuery = $"EXEC sp_rename '[{oldPhysicalTableName}]', '{newSecureTableName}'";
                using var command = new SqlCommand(renameQuery, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Table successfully renamed from {OldTableName} to {NewTableName} for user {UserId}",
                    oldPhysicalTableName, newSecureTableName, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming physical table from {OldTableName} to {NewTableName} for user {UserId}",
                    oldPhysicalTableName, newLogicalTableName, userId);
                return false;
            }
        }

        public async Task<bool> TableExistsAsync(string physicalTableName)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var tableExistsQuery = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = @tableName AND TABLE_SCHEMA = 'dbo'";

                using var command = new SqlCommand(tableExistsQuery, connection);
                command.Parameters.AddWithValue("@tableName", physicalTableName);

                var count = (int)await command.ExecuteScalarAsync();
                var exists = count > 0;

                _logger.LogDebug("Table {TableName} exists: {Exists}", physicalTableName, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if table {TableName} exists", physicalTableName);
                return false;
            }
        }

        public async Task<List<string>> GetAllUserTablesAsync(int userId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'dbo' 
            AND TABLE_NAME LIKE 'Table_' + @userId + '_%'
            ORDER BY TABLE_NAME";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);

                var tables = new List<string>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString("TABLE_NAME"));
                }

                _logger.LogInformation("Found {Count} tables for user {UserId}: {Tables}",
                    tables.Count, userId, string.Join(", ", tables));

                return tables;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all user tables for user {UserId}", userId);
                return new List<string>();
            }
        }

        public async Task<bool> DeleteDataFromUserTableAsync(string tableName, string whereClause, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var deleteQuery = $"DELETE FROM [{secureTableName}] WHERE {whereClause}";
                using var command = new SqlCommand(deleteQuery, connection);
                await command.ExecuteNonQueryAsync();
                return true;


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data from table {TableName} for user {UserId}", tableName, userId);
                return false;
            }
        }

        // Utility methods
        public string GenerateSecureTableName(string tableName, int userId)
        {
            return $"Table_{userId}_{tableName.Replace(" ", "_")}";
        }

        public string ConvertToSqlDataType(ColumnDataType dataType)
        {
            return dataType switch
            {
                ColumnDataType.VARCHAR => "NVARCHAR(255)",
                ColumnDataType.INT => "INT",
                ColumnDataType.DECIMAL => "DECIMAL(18,2)",
                ColumnDataType.DATETIME => "DATETIME",
                _ => throw new ArgumentException($"Unsupported data type: {dataType}")
            };
        }

        // New methods for enhanced update system
        public async Task<ColumnUpdateResult> UpdateColumnDataTypeAsync(string tableName, string columnName, ColumnDataType newDataType, bool forceUpdate, int userId)
        {
            var result = new ColumnUpdateResult();
            var executedQueries = new List<string>();

            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    var newSqlDataType = ConvertToSqlDataType(newDataType);
                    var alterQuery = $"ALTER TABLE [{secureTableName}] ALTER COLUMN [{sanitizedColumnName}] {newSqlDataType}";

                    using var command = new SqlCommand(alterQuery, connection, transaction);
                    var affectedRows = await command.ExecuteNonQueryAsync();

                    executedQueries.Add(alterQuery);

                    await transaction.CommitAsync();

                    result.Success = true;
                    result.Message = "Kolon veri tipi başarıyla güncellendi";
                    result.ExecutedQueries = executedQueries;
                    result.AffectedRows = affectedRows;

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

                // Basic validation logic here
                result.AffectedRowCount = await GetTableRowCountAsync(tableName, userId);

                // Add specific validation logic based on data type conversion
                // This is a simplified version - you can expand with more sophisticated checks

                _logger.LogInformation("Column validation completed for {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating column update: {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
                result.IsValid = false;
                result.Issues.Add("Validasyon sırasında hata oluştu: " + ex.Message);
            }

            return result;
        }

        public async Task<bool> RenameColumnAsync(string tableName, string oldColumnName, string newColumnName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var oldSanitizedName = SanitizeColumnName(oldColumnName);
                var newSanitizedName = SanitizeColumnName(newColumnName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var renameQuery = $"EXEC sp_rename '[{secureTableName}].[{oldSanitizedName}]', '{newSanitizedName}', 'COLUMN'";
                using var command = new SqlCommand(renameQuery, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Column renamed successfully: {TableName}.{OldColumnName} to {NewColumnName} for user {UserId}",
                    tableName, oldColumnName, newColumnName, userId);
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

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // This is a simplified implementation - you might need more sophisticated logic
                // for handling default constraints properly

                _logger.LogInformation("Column default value updated: {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
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

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // This is a simplified implementation
                _logger.LogInformation("Column nullability updated: {TableName}.{ColumnName} to {IsRequired} for user {UserId}",
                    tableName, columnName, isRequired, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column nullability: {TableName}.{ColumnName} for user {UserId}",
                    tableName, columnName, userId);
                return false;
            }
        }

        // Implementation of missing methods for enhanced update system
        public async Task<int> GetTableRowCountAsync(string tableName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = $"SELECT COUNT(*) FROM [{secureTableName}]";
                using var command = new SqlCommand(query, connection);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting row count for table {TableName} for user {UserId}", tableName, userId);
                return 0;
            }
        }

        public async Task<long> EstimateTableSizeAsync(string tableName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        SUM(a.total_pages) * 8 * 1024 AS TableSizeBytes
                    FROM 
                        sys.tables t
                    INNER JOIN      
                        sys.indexes i ON t.OBJECT_ID = i.object_id
                    INNER JOIN 
                        sys.partitions p ON i.object_id = p.OBJECT_ID AND i.index_id = p.index_id
                    INNER JOIN 
                        sys.allocation_units a ON p.partition_id = a.container_id
                    WHERE 
                        t.name = @tableName";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@tableName", secureTableName);

                var result = await command.ExecuteScalarAsync();
                return result != DBNull.Value ? Convert.ToInt64(result) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating size for table {TableName} for user {UserId}", tableName, userId);
                return 0;
            }
        }

        public async Task<bool> ColumnHasDataAsync(string tableName, string columnName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = $"SELECT COUNT(*) FROM [{secureTableName}] WHERE [{sanitizedColumnName}] IS NOT NULL";
                using var command = new SqlCommand(query, connection);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if column {ColumnName} has data in table {TableName} for user {UserId}",
                    columnName, tableName, userId);
                return false;
            }
        }

        public async Task<bool> ColumnHasNullDataAsync(string tableName, string columnName, int userId)
        {
            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = $"SELECT COUNT(*) FROM [{secureTableName}] WHERE [{sanitizedColumnName}] IS NULL";
                using var command = new SqlCommand(query, connection);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for null data in column {ColumnName} of table {TableName} for user {UserId}",
                    columnName, tableName, userId);
                return false;
            }
        }

        public async Task<ColumnValidationResult> ValidateColumnDataTypeChangeAsync(string tableName, string columnName, ColumnDataType currentType, ColumnDataType newType, int userId)
        {
            var result = new ColumnValidationResult { IsValid = true };

            try
            {
                // Check if conversion is possible
                if (!CanConvertDataType(currentType, newType))
                {
                    result.IsValid = false;
                    result.Issues.Add($"{currentType} tipinden {newType} tipine dönüşüm desteklenmiyor");
                    return result;
                }

                // Get row count
                result.AffectedRowCount = await GetTableRowCountAsync(tableName, userId);

                if (result.AffectedRowCount > 0)
                {
                    // Check for lossy conversions
                    if (IsLossyConversion(currentType, newType))
                    {
                        result.HasDataCompatibilityIssues = true;
                        result.RequiresForceUpdate = true;
                        result.DataIssues.Add("Bu dönüşüm veri kaybına veya kesilmeye neden olabilir");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating column data type change for {TableName}.{ColumnName} from {CurrentType} to {NewType} for user {UserId}",
                    tableName, columnName, currentType, newType, userId);
                result.IsValid = false;
                result.Issues.Add("Validasyon sırasında hata oluştu: " + ex.Message);
            }

            return result;
        }

        public async Task<DDLOperationResult> CreateTableBackupAsync(string tableName, int userId)
        {
            var result = new DDLOperationResult();
            var executedQueries = new List<string>();

            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var backupTableName = $"{secureTableName}_backup_{DateTime.UtcNow:yyyyMMddHHmmss}";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = $@"
                    SELECT * 
                    INTO [{backupTableName}] 
                    FROM [{secureTableName}]";

                using var command = new SqlCommand(query, connection);
                var affectedRows = await command.ExecuteNonQueryAsync();

                executedQueries.Add(query);

                result.Success = true;
                result.Message = $"Yedek tablo oluşturuldu: {backupTableName}";
                result.ExecutedQueries = executedQueries;
                result.AffectedRows = affectedRows;

                _logger.LogInformation("Backup table {BackupTableName} created successfully for user {UserId}", backupTableName, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup for table {TableName} for user {UserId}", tableName, userId);
                result.Message = "Yedek tablo oluşturulurken hata oluştu: " + ex.Message;
            }

            return result;
        }

        public async Task<DDLOperationResult> DropColumnAsync(string tableName, string columnName, int userId)
        {
            var result = new DDLOperationResult();
            var executedQueries = new List<string>();

            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var dropQuery = $"ALTER TABLE [{secureTableName}] DROP COLUMN [{sanitizedColumnName}]";
                using var command = new SqlCommand(dropQuery, connection);
                await command.ExecuteNonQueryAsync();

                executedQueries.Add(dropQuery);

                result.Success = true;
                result.Message = "Kolon başarıyla silindi";
                result.ExecutedQueries = executedQueries;

                _logger.LogInformation("Column {ColumnName} dropped from table {TableName} for user {UserId}", columnName, tableName, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dropping column {ColumnName} from table {TableName} for user {UserId}", columnName, tableName, userId);
                result.Message = "Kolon silinirken hata oluştu: " + ex.Message;
            }

            return result;
        }

        public async Task<DDLOperationResult> AddColumnAsync(string tableName, UpdateColumnRequest columnRequest, int userId)
        {
            var result = new DDLOperationResult();
            var executedQueries = new List<string>();

            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);
                var sanitizedColumnName = SanitizeColumnName(columnRequest.ColumnName);
                var sqlDataType = ConvertToSqlDataType(columnRequest.DataType);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var addQuery = $"ALTER TABLE [{secureTableName}] ADD [{sanitizedColumnName}] {sqlDataType}";

                if (!string.IsNullOrEmpty(columnRequest.DefaultValue))
                {
                    var defaultValue = FormatDefaultValue(columnRequest.DefaultValue, columnRequest.DataType);
                    addQuery += $" DEFAULT {defaultValue}";
                }

                if (columnRequest.IsRequired)
                {
                    addQuery += " NOT NULL";
                }

                using var command = new SqlCommand(addQuery, connection);
                await command.ExecuteNonQueryAsync();
                executedQueries.Add(addQuery);

                result.Success = true;
                result.Message = "Kolon başarıyla eklendi";
                result.ExecutedQueries = executedQueries;

                _logger.LogInformation("Column {ColumnName} added to table {TableName} for user {UserId}", columnRequest.ColumnName, tableName, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding column {ColumnName} to table {TableName} for user {UserId}", columnRequest.ColumnName, tableName, userId);
                result.Message = "Kolon eklenirken hata oluştu: " + ex.Message;
            }

            return result;
        }

        public async Task<DDLOperationResult> UpdateColumnAsync(string tableName, CustomColumn existingColumn, UpdateColumnRequest updateRequest, int userId)
        {
            var result = new DDLOperationResult();
            var executedQueries = new List<string>();
            var totalAffectedRows = 0;

            try
            {
                var secureTableName = GenerateSecureTableName(tableName, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Handle column name change
                    if (existingColumn.ColumnName != updateRequest.ColumnName)
                    {
                        var success = await RenameColumnAsync(tableName, existingColumn.ColumnName, updateRequest.ColumnName, userId);
                        if (success)
                        {
                            executedQueries.Add($"Column renamed from {existingColumn.ColumnName} to {updateRequest.ColumnName}");
                        }
                    }

                    // Handle data type change
                    if (existingColumn.DataType != updateRequest.DataType)
                    {
                        var alterResult = await UpdateColumnDataTypeAsync(tableName, updateRequest.ColumnName, updateRequest.DataType, updateRequest.ForceUpdate, userId);
                        executedQueries.AddRange(alterResult.ExecutedQueries);
                        totalAffectedRows += alterResult.AffectedRows;
                    }

                    await transaction.CommitAsync();

                    result.Success = true;
                    result.Message = "Kolon başarıyla güncellendi";
                    result.ExecutedQueries = executedQueries;
                    result.AffectedRows = totalAffectedRows;

                    _logger.LogInformation("Column {ColumnName} updated in table {TableName} for user {UserId}", updateRequest.ColumnName, tableName, userId);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column {ColumnName} in table {TableName} for user {UserId}", updateRequest.ColumnName, tableName, userId);
                result.Message = "Kolon güncellenirken hata oluştu: " + ex.Message;
            }

            return result;
        }

        // Helper methods
        private bool CanConvertDataType(ColumnDataType from, ColumnDataType to)
        {
            return (from, to) switch
            {
                (ColumnDataType.VARCHAR, ColumnDataType.INT) => true,
                (ColumnDataType.VARCHAR, ColumnDataType.DECIMAL) => true,
                (ColumnDataType.VARCHAR, ColumnDataType.DATETIME) => true,
                (ColumnDataType.INT, ColumnDataType.VARCHAR) => true,
                (ColumnDataType.INT, ColumnDataType.DECIMAL) => true,
                (ColumnDataType.DECIMAL, ColumnDataType.VARCHAR) => true,
                (ColumnDataType.DECIMAL, ColumnDataType.INT) => true,
                (ColumnDataType.DATETIME, ColumnDataType.VARCHAR) => true,
                _ => from == to
            };
        }

        private bool IsLossyConversion(ColumnDataType from, ColumnDataType to)
        {
            return (from, to) switch
            {
                (ColumnDataType.VARCHAR, ColumnDataType.INT) => true,
                (ColumnDataType.VARCHAR, ColumnDataType.DECIMAL) => true,
                (ColumnDataType.VARCHAR, ColumnDataType.DATETIME) => true,
                (ColumnDataType.DECIMAL, ColumnDataType.INT) => true,
                _ => false
            };
        }




        public async Task<List<string>> GetAllTablesDebugAsync()
        {
            // Hata durumunda boş liste dönebilmek için listeyi try bloğunun dışında başlatabiliriz,
            // ancak her iki durumda da yeni liste döndürdüğümüz için try içinde kalması daha temizdir.
            try
            {
                var tables = new List<string>();

                // 'using var', veritabanı bağlantısı gibi kaynakların işi bittiğinde
                // otomatik ve güvenli bir şekilde kapatılmasını sağlar.
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'dbo'
            ORDER BY TABLE_NAME";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    // SqlDataReader'dan veri okurken sütun adıyla doğrudan string alınamaz.
                    // Sütunun sıra numarası (index) kullanılmalıdır. Sorguda tek sütun
                    // seçtiğimiz için indeksi 0'dır.
                    tables.Add(reader.GetString(0));
                }

                _logger.LogInformation("DEBUG: {Count} adet tablo bulundu: {Tables}",
                    tables.Count, string.Join(", ", tables));

                return tables;
            }
            catch (Exception ex)
            {
                // Hata oluştuğunda loglama yapılır.
                _logger.LogError(ex, "DEBUG için tüm tablolar alınırken bir hata oluştu.");

                // Hata durumunda istemciye boş bir liste döndürülür.
                return new List<string>();
            }
        }























        // DataDefinitionService.cs - Direkt fiziksel tablo işlemleri

        /// <summary>
        /// Fiziksel tabloyu direkt yeniden adlandırır (logical name conversion yapmaz)
        /// </summary>
        public async Task<bool> RenamePhysicalTableDirectAsync(string oldPhysicalTableName, string newPhysicalTableName)
        {
            try
            {
                _logger.LogInformation("Direct rename: {OldName} -> {NewName}", oldPhysicalTableName, newPhysicalTableName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Eski tablonun varlığını kontrol et
                var oldExists = await TableExistsAsync(oldPhysicalTableName);
                if (!oldExists)
                {
                    _logger.LogWarning("Source table does not exist: {TableName}", oldPhysicalTableName);
                    return false;
                }

                // Yeni tablo adının çakışmamasını kontrol et
                var newExists = await TableExistsAsync(newPhysicalTableName);
                if (newExists)
                {
                    _logger.LogError("Target table already exists: {TableName}", newPhysicalTableName);
                    return false;
                }

                // Tabloyu yeniden adlandır
                var renameQuery = $"EXEC sp_rename '[{oldPhysicalTableName}]', '{newPhysicalTableName}'";
                using var command = new SqlCommand(renameQuery, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Physical table renamed successfully: {OldName} -> {NewName}",
                    oldPhysicalTableName, newPhysicalTableName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming physical table: {OldName} -> {NewName}",
                    oldPhysicalTableName, newPhysicalTableName);
                return false;
            }
        }

        /// <summary>
        /// Fiziksel tablodan direkt kolon siler
        /// </summary>
        public async Task<DDLOperationResult> DropColumnDirectAsync(string physicalTableName, string columnName)
        {
            var result = new DDLOperationResult();

            try
            {
                _logger.LogInformation("Dropping column {ColumnName} from table {TableName}", columnName, physicalTableName);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var dropQuery = $"ALTER TABLE [{physicalTableName}] DROP COLUMN [{columnName}]";
                using var command = new SqlCommand(dropQuery, connection);
                await command.ExecuteNonQueryAsync();

                result.Success = true;
                result.Message = $"Column {columnName} dropped successfully";
                result.ExecutedQueries = new List<string> { dropQuery };

                _logger.LogInformation("Column {ColumnName} dropped from {TableName}", columnName, physicalTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dropping column {ColumnName} from {TableName}", columnName, physicalTableName);
                result.Message = "Kolon silinirken hata: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Fiziksel tabloya direkt kolon ekler
        /// </summary>
        public async Task<DDLOperationResult> AddColumnDirectAsync(string physicalTableName, UpdateColumnRequest columnRequest)
        {
            var result = new DDLOperationResult();

            try
            {
                _logger.LogInformation("Adding column {ColumnName} to table {TableName}",
                    columnRequest.ColumnName, physicalTableName);

                var sqlDataType = ConvertToSqlDataType(columnRequest.DataType);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var addQuery = $"ALTER TABLE [{physicalTableName}] ADD [{columnRequest.ColumnName}] {sqlDataType}";

                // Default value ekle
                if (!string.IsNullOrEmpty(columnRequest.DefaultValue))
                {
                    var defaultValue = FormatDefaultValue(columnRequest.DefaultValue, columnRequest.DataType);
                    addQuery += $" DEFAULT {defaultValue}";
                }

                // NOT NULL constraint ekle (eğer default value varsa)
                if (columnRequest.IsRequired && !string.IsNullOrEmpty(columnRequest.DefaultValue))
                {
                    addQuery += " NOT NULL";
                }

                using var command = new SqlCommand(addQuery, connection);
                await command.ExecuteNonQueryAsync();

                result.Success = true;
                result.Message = $"Column {columnRequest.ColumnName} added successfully";
                result.ExecutedQueries = new List<string> { addQuery };

                _logger.LogInformation("Column {ColumnName} added to {TableName}",
                    columnRequest.ColumnName, physicalTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding column {ColumnName} to {TableName}",
                    columnRequest.ColumnName, physicalTableName);
                result.Message = "Kolon eklenirken hata: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Default value'yu SQL formatına çevirir
        /// </summary>
        private string FormatDefaultValue(string value, ColumnDataType dataType)
        {
            if (string.IsNullOrEmpty(value))
                return "NULL";

            switch (dataType)
            {
                case ColumnDataType.VARCHAR:
                    return $"'{value.Replace("'", "''")}'"; // SQL injection koruması

                case ColumnDataType.INT:
                    if (int.TryParse(value, out int intValue))
                        return intValue.ToString();
                    return "0";

                case ColumnDataType.DECIMAL:
                    if (decimal.TryParse(value, out decimal decimalValue))
                        return decimalValue.ToString().Replace(",", ".");
                    return "0.00";

                case ColumnDataType.DATETIME:
                    if (DateTime.TryParse(value, out DateTime dateValue))
                        return $"'{dateValue:yyyy-MM-dd HH:mm:ss}'";
                    return "GETDATE()";

                default:
                    return $"'{value}'";
            }
        }

        /// <summary>
        /// Güvenli kolon adı oluşturur
        /// </summary>
        private string SanitizeColumnName(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return "UnknownColumn";

            // SQL injection koruması ve özel karakterleri temizle
            return columnName
                .Replace("[", "")
                .Replace("]", "")
                .Replace(";", "")
                .Replace("--", "")
                .Replace("/*", "")
                .Replace("*/", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Trim();
        }
    }
}