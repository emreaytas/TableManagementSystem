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
        private readonly IDataDefinitionService _dataDefinitionService;

        public TableService(IUnitOfWork unitOfWork, IMapper mapper, IDataDefinitionService dataDefinitionService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dataDefinitionService = dataDefinitionService;
        }

        public async Task<TableResponse> CreateTableAsync(CreateTableRequest request, int userId)
        {
            await _unitOfWork.BeginTransactionAsync(); // Corrected: Removed 'using var transaction' as BeginTransactionAsync returns void.  

            try
            {
                // Check if table name exists for user  
                var exists = await _unitOfWork.CustomTables.TableNameExistsForUserAsync(request.TableName, userId);
                if (exists)
                {
                    throw new ArgumentException("Bu tablo adı zaten kullanılıyor.");
                }

                // Create metadata table  
                var table = new CustomTable
                {
                    TableName = request.TableName,
                    Description = request.Description,
                    UserId = userId
                };

                await _unitOfWork.CustomTables.AddAsync(table);
                await _unitOfWork.SaveChangesAsync();

                // Create columns metadata  
                var columns = new List<CustomColumn>();
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
                    columns.Add(column);
                }

                await _unitOfWork.SaveChangesAsync();

                // Create actual database table using DDL  
                var ddlResult = await _dataDefinitionService.CreateUserTableAsync(table.TableName, columns, userId);
                if (!ddlResult)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw new ApplicationException("Veritabanında tablo oluşturulamadı.");
                }

                await _unitOfWork.CommitTransactionAsync();

                var createdTable = await _unitOfWork.CustomTables.GetTableWithColumnsAsync(table.Id);
                return _mapper.Map<TableResponse>(createdTable);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
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

            try
            {
                // Delete actual database table first
                var ddlResult = await _dataDefinitionService.DropUserTableAsync(table.TableName, userId);
                if (!ddlResult)
                {
                    // Log warning but continue with metadata deletion
                    // Bu durumda metadata'yı silmek isteyebilirsiniz
                }

                // Delete metadata
                await _unitOfWork.CustomTables.DeleteAsync(table);
                await _unitOfWork.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<TableDataResponse> GetTableDataAsync(int tableId, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetTableWithColumnsAsync(tableId);
            if (table == null || table.UserId != userId)
            {
                return null;
            }

            try
            {
                // Get data from actual database table
                var tableData = await _dataDefinitionService.SelectDataFromUserTableAsync(table.TableName, userId);

                var response = new TableDataResponse
                {
                    TableId = table.Id,
                    TableName = table.TableName,
                    Columns = _mapper.Map<List<ColumnResponse>>(table.Columns.OrderBy(c => c.DisplayOrder))
                };

                // Convert data to expected format
                foreach (var row in tableData)
                {
                    var tableRow = new TableRowResponse
                    {
                        RowIdentifier = (int)(row.ContainsKey("Id") ? row["Id"] : 0),
                        Values = new Dictionary<int, string>()
                    };

                    // Map column data to column IDs
                    foreach (var column in table.Columns)
                    {
                        if (row.ContainsKey(column.ColumnName))
                        {
                            tableRow.Values[column.Id] = row[column.ColumnName]?.ToString() ?? string.Empty;
                        }
                    }

                    response.Rows.Add(tableRow);
                }

                return response;
            }
            catch (Exception)
            {
                return null;
            }
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

                // Convert column IDs to column names with values
                var data = new Dictionary<string, object>();
                foreach (var columnValue in request.ColumnValues)
                {
                    var column = table.Columns.FirstOrDefault(c => c.Id == columnValue.Key);
                    if (column != null)
                    {
                        // Convert value to appropriate type
                        var convertedValue = ConvertValueToType(columnValue.Value, column.DataType);
                        data[column.ColumnName] = convertedValue;
                    }
                }

                // Insert data to actual database table
                return await _dataDefinitionService.InsertDataToUserTableAsync(table.TableName, data, userId);
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
                var table = await _unitOfWork.CustomTables.GetTableWithColumnsAsync(tableId);
                if (table == null || table.UserId != userId)
                {
                    return false;
                }

                // Convert column IDs to column names with values
                var data = new Dictionary<string, object>();
                foreach (var value in values)
                {
                    var column = table.Columns.FirstOrDefault(c => c.Id == value.Key);
                    if (column != null)
                    {
                        var convertedValue = ConvertValueToType(value.Value, column.DataType);
                        data[column.ColumnName] = convertedValue;
                    }
                }

                var whereClause = $"Id = {rowIdentifier}";
                return await _dataDefinitionService.UpdateDataInUserTableAsync(table.TableName, data, whereClause, userId);
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

                var whereClause = $"Id = {rowIdentifier}";
                return await _dataDefinitionService.DeleteDataFromUserTableAsync(table.TableName, whereClause, userId);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private object ConvertValueToType(string value, Core.Enums.ColumnDataType dataType)
        {
            if (string.IsNullOrEmpty(value))
                return DBNull.Value;

            return dataType switch
            {
                Core.Enums.ColumnDataType.Int => int.TryParse(value, out int intValue) ? intValue : DBNull.Value,
                Core.Enums.ColumnDataType.Decimal => decimal.TryParse(value, out decimal decimalValue) ? decimalValue : DBNull.Value,
                Core.Enums.ColumnDataType.DateTime => DateTime.TryParse(value, out DateTime dateValue) ? dateValue : DBNull.Value,
                Core.Enums.ColumnDataType.Varchar => value,
                _ => value
            };
        }
    }
}