using AutoMapper;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;

namespace TableManagement.Application.Services
{
    public class TableService : ITableService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public TableService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<TableResponse> CreateTableAsync(CreateTableRequest request, int userId)
        {
            try
            {
                // Check if table name exists for user
                var exists = await _unitOfWork.CustomTables.TableNameExistsForUserAsync(request.TableName, userId);
                if (exists)
                {
                    throw new ArgumentException("Bu tablo adı zaten kullanılıyor.");
                }

                var table = new CustomTable
                {
                    TableName = request.TableName,
                    Description = request.Description,
                    UserId = userId
                };

                await _unitOfWork.CustomTables.AddAsync(table);
                await _unitOfWork.SaveChangesAsync();

                // Add columns
                foreach (var columnRequest in request.Columns)
                {
                    var column = new CustomColumn
                    {
                        ColumnName = columnRequest.ColumnName,
                        DataType = columnRequest.DataType,
                        IsRequired = columnRequest.IsRequired,
                        DisplayOrder = columnRequest.DisplayOrder,
                        DefaultValue = columnRequest.DefaultValue,
                        CustomTableId = table.Id
                    };

                    await _unitOfWork.Repository<CustomColumn>().AddAsync(column);
                }

                await _unitOfWork.SaveChangesAsync();

                var createdTable = await _unitOfWork.CustomTables.GetTableWithColumnsAsync(table.Id);
                return _mapper.Map<TableResponse>(createdTable);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Tablo oluşturulurken bir hata oluştu.", ex);
            }
        }

        public async Task<IEnumerable<TableResponse>> GetUserTablesAsync(int userId)
        {
            var tables = await _unitOfWork.CustomTables.GetUserTablesAsync(userId);
            return _mapper.Map<IEnumerable<TableResponse>>(tables);
        }

        public async Task<TableResponse> GetTableByIdAsync(int tableId, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetTableWithColumnsAsync(tableId);
            if (table == null || table.UserId != userId)
            {
                return null;
            }

            return _mapper.Map<TableResponse>(table);
        }

        public async Task<bool> DeleteTableAsync(int tableId, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetByIdAsync(tableId);
            if (table == null || table.UserId != userId)
            {
                return false;
            }

            await _unitOfWork.CustomTables.DeleteAsync(table);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<TableDataResponse> GetTableDataAsync(int tableId, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetTableWithDataAsync(tableId);
            if (table == null || table.UserId != userId)
            {
                return null;
            }

            var response = new TableDataResponse
            {
                TableId = table.Id,
                TableName = table.TableName,
                Columns = _mapper.Map<List<ColumnResponse>>(table.Columns.OrderBy(c => c.DisplayOrder))
            };

            // Group data by row identifier
            var groupedData = table.TableData
                .GroupBy(td => td.RowIdentifier)
                .OrderBy(g => g.Key);

            foreach (var rowGroup in groupedData)
            {
                var row = new TableRowResponse
                {
                    RowIdentifier = rowGroup.Key,
                    Values = rowGroup.ToDictionary(td => td.ColumnId, td => td.Value)
                };
                response.Rows.Add(row);
            }

            return response;
        }

        public async Task<bool> AddTableDataAsync(AddTableDataRequest request, int userId)
        {
            try
            {
                var table = await _unitOfWork.CustomTables.GetTableWithColumnsAsync(request.TableId);
                if (table == null || table.UserId != userId)
                {
                    return false;
                }

                var rowIdentifier = await _unitOfWork.CustomTableData.GetNextRowIdentifierAsync(request.TableId);

                foreach (var columnValue in request.ColumnValues)
                {
                    var tableData = new CustomTableData
                    {
                        CustomTableId = request.TableId,
                        ColumnId = columnValue.Key,
                        Value = columnValue.Value,
                        RowIdentifier = rowIdentifier
                    };

                    await _unitOfWork.CustomTableData.AddAsync(tableData);
                }

                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> UpdateTableDataAsync(int tableId, int rowIdentifier, Dictionary<int, string> values, int userId)
        {
            try
            {
                var table = await _unitOfWork.CustomTables.GetByIdAsync(tableId);
                if (table == null || table.UserId != userId)
                {
                    return false;
                }

                var existingData = await _unitOfWork.CustomTableData.GetRowDataAsync(tableId, rowIdentifier);

                foreach (var data in existingData)
                {
                    if (values.ContainsKey(data.ColumnId))
                    {
                        data.Value = values[data.ColumnId];
                        data.UpdatedAt = DateTime.UtcNow;
                        await _unitOfWork.CustomTableData.UpdateAsync(data);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeleteTableDataAsync(int tableId, int rowIdentifier, int userId)
        {
            try
            {
                var table = await _unitOfWork.CustomTables.GetByIdAsync(tableId);
                if (table == null || table.UserId != userId)
                {
                    return false;
                }

                await _unitOfWork.CustomTableData.DeleteRowDataAsync(tableId, rowIdentifier);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}