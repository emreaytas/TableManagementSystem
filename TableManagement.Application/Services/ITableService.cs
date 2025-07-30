using DevExtreme.AspNet.Data.ResponseModel;
using System.IO.Pipelines;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;
using DevExtreme.AspNet.Data.ResponseModel;

namespace TableManagement.Application.Services
{
    public interface ITableService
    {
        Task<TableResponse> CreateTableAsync(CreateTableRequest request, int userId);
        Task<IEnumerable<TableResponse>> GetUserTablesAsync(int userId);
        Task<TableResponse> GetTableByIdAsync(int tableId, int userId);
        Task<bool> DeleteTableAsync(int tableId, int userId);

        Task<TableDataResponse> GetTableDataAsync(int tableId, int userId);
        Task<bool> AddTableDataAsync(AddTableDataRequest request, int userId);

        Task<bool> UpdateTableDataAsync(UpdateTableDataRequest request, int userId);

        Task<bool> DeleteTableDataAsync(int tableId, int rowIdentifier, int userId);

        Task<bool> AddTableDataByIdAsync(AddTableDataByIdRequest request, int userId);
        Task<bool> UpdateTableDataByIdAsync(int tableId, int rowIdentifier, Dictionary<int, string> values, int userId);

        Task<ColumnUpdateResult> UpdateColumnAsync(int tableId, UpdateColumnRequest request, int userId);
        Task<ColumnValidationResult> ValidateColumnUpdateAsync(int tableId, UpdateColumnRequest request, int userId);

        Task<TableValidationResult> ValidateTableUpdateAsync(int tableId, ValidateTableUpdateRequest request, int userId);
        
        
        Task<TableUpdateResult> UpdateTableAsync(int tableId, UpdateTableRequest request, int userId);
        
        
        Task<TableCreateResult> CreateTableWithValidationAsync(CreateTableRequest request, int userId);



        // kullanıcıya ait tabloları DevExpress için listele. sonra front için güncelleme gelecek unutma emmi.
        Task<IEnumerable<TableListDto>> GetUserTablesForDevExpressAsync(int userId);



        



    }


    public class ColumnUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> ExecutedQueries { get; set; } = new();
        public int AffectedRows { get; set; }
        public ColumnValidationResult? ValidationResult { get; set; }
    }

    public class ColumnValidationResult
    {
        public bool IsValid { get; set; } = true;
        public bool HasDataCompatibilityIssues { get; set; }
        public bool RequiresForceUpdate { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> DataIssues { get; set; } = new();
        public int AffectedRowCount { get; set; }
    }

    public class TableValidationResult
    {
        public bool IsValid { get; set; } = true;
        public bool HasStructuralChanges { get; set; }
        public bool HasDataCompatibilityIssues { get; set; }
        public bool RequiresForceUpdate { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> DataIssues { get; set; } = new();
        public Dictionary<string, List<string>> ColumnIssues { get; set; } = new();
        public int AffectedRowCount { get; set; }
        public long EstimatedBackupSize { get; set; }
    }

    public class TableUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Table { get; set; }
        public List<string> ExecutedQueries { get; set; } = new();
        public int AffectedRows { get; set; }
        public bool BackupCreated { get; set; }
        public TableValidationResult? ValidationResult { get; set; }
    }

    public class TableCreateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Table { get; set; }
        public List<string> ExecutedQueries { get; set; } = new();
    }
}