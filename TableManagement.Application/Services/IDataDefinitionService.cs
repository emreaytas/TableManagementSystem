using TableManagement.Application.DTOs.Responses;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Core.Entities;
using TableManagement.Core.Enums;

namespace TableManagement.Application.Services
{
    public interface IDataDefinitionService
    {
        // Existing methods
        Task<bool> CreateUserTableAsync(string tableName, List<CustomColumn> columns, int userId);
        Task<bool> DropUserTableAsync(string tableName, int userId);
        Task<bool> InsertDataToUserTableAsync(string tableName, Dictionary<string, object> data, int userId);
        Task<List<Dictionary<string, object>>> SelectDataFromUserTableAsync(string tableName, int userId);
        Task<bool> UpdateDataInUserTableAsync(string tableName, Dictionary<string, object> data, string whereClause, int userId);
        Task<bool> DeleteDataFromUserTableAsync(string tableName, string whereClause, int userId);

        // Utility methods
        string GenerateSecureTableName(string tableName, int userId);
        string ConvertToSqlDataType(ColumnDataType dataType);

        // Column operations
        Task<ColumnUpdateResult> UpdateColumnDataTypeAsync(string tableName, string columnName, ColumnDataType newDataType, bool forceUpdate, int userId);
        Task<ColumnValidationResult> ValidateColumnUpdateAsync(string tableName, string columnName, ColumnDataType newDataType, int userId);
        Task<bool> RenameColumnAsync(string tableName, string oldColumnName, string newColumnName, int userId);
        Task<bool> UpdateColumnDefaultValueAsync(string tableName, string columnName, string defaultValue, int userId);
        Task<bool> UpdateColumnNullabilityAsync(string tableName, string columnName, bool isRequired, int userId);

        // New methods for enhanced update system
        Task<int> GetTableRowCountAsync(string tableName, int userId);
        Task<long> EstimateTableSizeAsync(string tableName, int userId);
        Task<bool> ColumnHasDataAsync(string tableName, string columnName, int userId);
        Task<bool> ColumnHasNullDataAsync(string tableName, string columnName, int userId);
        Task<ColumnValidationResult> ValidateColumnDataTypeChangeAsync(string tableName, string columnName, ColumnDataType currentType, ColumnDataType newType, int userId);
        Task<DDLOperationResult> CreateTableBackupAsync(string tableName, int userId);
        Task<DDLOperationResult> DropColumnAsync(string tableName, string columnName, int userId);
        Task<DDLOperationResult> AddColumnAsync(string tableName, UpdateColumnRequest columnRequest, int userId);
        Task<DDLOperationResult> UpdateColumnAsync(string tableName, CustomColumn existingColumn, UpdateColumnRequest updateRequest, int userId);
    }

    // Result class for DDL operations
    public class DDLOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? ExecutedQueries { get; set; }
        public int AffectedRows { get; set; }
        public ColumnValidationResult? ValidationResult { get; set; }
    }
}