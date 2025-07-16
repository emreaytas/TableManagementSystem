using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;

namespace TableManagement.Application.Services
{
    public interface ITableService
    {
        // Mevcut metod tanımları
        Task<TableResponse> CreateTableAsync(CreateTableRequest request, int userId);
        Task<IEnumerable<TableResponse>> GetUserTablesAsync(int userId);
        Task<TableResponse> GetTableByIdAsync(int tableId, int userId);
        Task<bool> DeleteTableAsync(int tableId, int userId);
        Task<TableDataResponse> GetTableDataAsync(int tableId, int userId);
        Task<bool> AddTableDataAsync(AddTableDataRequest request, int userId);
        Task<bool> UpdateTableDataAsync(int tableId, int rowIdentifier, Dictionary<int, string> values, int userId);
        Task<bool> DeleteTableDataAsync(int tableId, int rowIdentifier, int userId);

        // Yeni kolon güncelleme metod tanımları
        Task<ColumnUpdateResult> UpdateColumnAsync(int tableId, UpdateColumnRequest request, int userId);
        Task<ColumnValidationResult> ValidateColumnUpdateAsync(int tableId, UpdateColumnRequest request, int userId);
    }
}