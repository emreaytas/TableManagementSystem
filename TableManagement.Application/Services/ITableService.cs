using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;

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
        Task<bool> UpdateTableDataAsync(int tableId, int rowIdentifier, Dictionary<int, string> values, int userId);
        Task<bool> DeleteTableDataAsync(int tableId, int rowIdentifier, int userId);
    }
}