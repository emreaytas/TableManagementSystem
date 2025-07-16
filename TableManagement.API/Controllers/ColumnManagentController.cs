using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.Services;
using TableManagement.Core.Enums;

namespace TableManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ColumnManagementController : ControllerBase
    {
        private readonly ITableService _tableService;
        private readonly IDataDefinitionService _dataDefinitionService;
        private readonly ILogger<ColumnManagementController> _logger;

        public ColumnManagementController(
            ITableService tableService,
            IDataDefinitionService dataDefinitionService,
            ILogger<ColumnManagementController> logger)
        {
            _tableService = tableService;
            _dataDefinitionService = dataDefinitionService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        /// <summary>
        /// Kolon güncelleme işleminden önce validasyon yapar
        /// </summary>
        [HttpPost("validate/{tableId}")]
        public async Task<IActionResult> ValidateColumnUpdate(int tableId, [FromBody] UpdateColumnRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                var validationResult = await _tableService.ValidateColumnUpdateAsync(tableId, request, userId);

                return Ok(new
                {
                    isValid = validationResult.IsValid,
                    hasDataCompatibilityIssues = validationResult.HasDataCompatibilityIssues,
                    requiresForceUpdate = validationResult.RequiresForceUpdate,
                    issues = validationResult.Issues,
                    dataIssues = validationResult.DataIssues,
                    affectedRowCount = validationResult.AffectedRowCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating column update for table {TableId} and user {UserId}", tableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Validasyon sırasında hata oluştu." });
            }
        }

        /// <summary>
        /// Kolon güncelleme işlemini gerçekleştirir
        /// </summary>
        [HttpPut("{tableId}/columns")]
        public async Task<IActionResult> UpdateColumn(int tableId, [FromBody] UpdateColumnRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Updating column {ColumnId} in table {TableId} for user {UserId}",
                    request.ColumnId, tableId, userId);

                var result = await _tableService.UpdateColumnAsync(tableId, request, userId);

                if (!result.Success)
                {
                    if (result.ValidationResult?.RequiresForceUpdate == true && !request.ForceUpdate)
                    {
                        return BadRequest(new
                        {
                            message = result.Message,
                            requiresForceUpdate = true,
                            validationResult = result.ValidationResult
                        });
                    }

                    return BadRequest(new { message = result.Message });
                }

                _logger.LogInformation("Column {ColumnId} updated successfully in table {TableId} for user {UserId}",
                    request.ColumnId, tableId, userId);

                return Ok(new
                {
                    message = result.Message,
                    success = true,
                    executedQueries = result.ExecutedQueries,
                    validationResult = result.ValidationResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column {ColumnId} in table {TableId} for user {UserId}",
                    request.ColumnId, tableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Kolon güncellenirken hata oluştu." });
            }
        }

        /// <summary>
        /// Kolon veri tipi değiştirme işlemini gerçekleştirir
        /// </summary>

        [HttpPut("{tableId}/columns/{columnId}/datatype")]
        public async Task<IActionResult> UpdateColumnDataType(int tableId, int columnId, [FromBody] UpdateColumnDataTypeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();

                var table = await _tableService.GetTableByIdAsync(tableId, userId);
                if (table == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                var column = table.Columns.FirstOrDefault(c => c.Id == columnId);
                if (column == null)
                {
                    return NotFound(new { message = "Kolon bulunamadı." });
                }

                // INT'i ColumnDataType'a dönüştür
                var newDataType = (ColumnDataType)request.NewDataType;

                // Enum'un geçerli olup olmadığını kontrol et
                if (!Enum.IsDefined(typeof(ColumnDataType), newDataType))
                {
                    return BadRequest(new { message = "Geçersiz veri tipi." });
                }

                _logger.LogInformation("Updating column data type {ColumnName} from {OldType} to {NewType} in table {TableId} for user {UserId}",
                    column.ColumnName, column.DataType, newDataType, tableId, userId);

                // DDL işlemini gerçekleştir
                var result = await _dataDefinitionService.UpdateColumnDataTypeAsync(
                    table.TableName, column.ColumnName, newDataType, request.ForceUpdate, userId);

                if (!result.Success)
                {
                    if (result.ValidationResult?.RequiresForceUpdate == true && !request.ForceUpdate)
                    {
                        return BadRequest(new
                        {
                            message = result.Message,
                            requiresForceUpdate = true,
                            validationResult = result.ValidationResult
                        });
                    }

                    return BadRequest(new { message = result.Message });
                }

                // Metadata'yı güncelle
                var updateRequest = new UpdateColumnRequest
                {
                    ColumnId = columnId,
                    ColumnName = column.ColumnName,
                    DataType = newDataType, // Artık doğru tip
                    IsRequired = column.IsRequired,
                    DisplayOrder = column.DisplayOrder,
                    DefaultValue = column.DefaultValue,
                    ForceUpdate = request.ForceUpdate
                };

                await _tableService.UpdateColumnAsync(tableId, updateRequest, userId);

                _logger.LogInformation("Column data type updated successfully for {ColumnName} in table {TableId} for user {UserId}",
                    column.ColumnName, tableId, userId);

                return Ok(new
                {
                    message = result.Message,
                    success = true,
                    executedQueries = result.ExecutedQueries,
                    validationResult = result.ValidationResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column data type for column {ColumnId} in table {TableId} for user {UserId}",
                    columnId, tableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Kolon veri tipi güncellenirken hata oluştu." });
            }
        }

        /// <summary>
        /// Kolon adını değiştirir
        /// </summary>
        [HttpPut("{tableId}/columns/{columnId}/name")]
        public async Task<IActionResult> RenameColumn(int tableId, int columnId, [FromBody] RenameColumnRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();

                var table = await _tableService.GetTableByIdAsync(tableId, userId);
                if (table == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                var column = table.Columns.FirstOrDefault(c => c.Id == columnId);
                if (column == null)
                {
                    return NotFound(new { message = "Kolon bulunamadı." });
                }

                _logger.LogInformation("Renaming column {OldName} to {NewName} in table {TableId} for user {UserId}",
                    column.ColumnName, request.NewColumnName, tableId, userId);

                var result = await _dataDefinitionService.RenameColumnAsync(
                    table.TableName, column.ColumnName, request.NewColumnName, userId);

                if (!result)
                {
                    return BadRequest(new { message = "Kolon adı güncellenemedi." });
                }

                // Metadata'yı güncelle
                var updateRequest = new UpdateColumnRequest
                {
                    ColumnId = columnId,
                    ColumnName = request.NewColumnName,
                    DataType = column.DataType,
                    IsRequired = column.IsRequired,
                    DisplayOrder = column.DisplayOrder,
                    DefaultValue = column.DefaultValue
                };

                await _tableService.UpdateColumnAsync(tableId, updateRequest, userId);

                _logger.LogInformation("Column renamed successfully from {OldName} to {NewName} in table {TableId} for user {UserId}",
                    column.ColumnName, request.NewColumnName, tableId, userId);

                return Ok(new
                {
                    message = "Kolon adı başarıyla güncellendi",
                    success = true,
                    oldName = column.ColumnName,
                    newName = request.NewColumnName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming column {ColumnId} in table {TableId} for user {UserId}",
                    columnId, tableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Kolon adı güncellenirken hata oluştu." });
            }
        }

        /// <summary>
        /// Kolon veri tipi dönüşüm matrisini getirir
        /// </summary>
        [HttpGet("conversion-matrix")]
        public IActionResult GetConversionMatrix()
        {
            var matrix = new Dictionary<string, Dictionary<string, object>>
            {
                ["Varchar"] = new Dictionary<string, object>
                {
                    ["Int"] = new { canConvert = true, isLossy = false, requiresValidation = true },
                    ["Decimal"] = new { canConvert = true, isLossy = false, requiresValidation = true },
                    ["DateTime"] = new { canConvert = true, isLossy = false, requiresValidation = true },
                    ["Varchar"] = new { canConvert = true, isLossy = false, requiresValidation = false }
                },
                ["Int"] = new Dictionary<string, object>
                {
                    ["Varchar"] = new { canConvert = true, isLossy = false, requiresValidation = false },
                    ["Decimal"] = new { canConvert = true, isLossy = false, requiresValidation = false },
                    ["DateTime"] = new { canConvert = false, isLossy = true, requiresValidation = true },
                    ["Int"] = new { canConvert = true, isLossy = false, requiresValidation = false }
                },
                ["Decimal"] = new Dictionary<string, object>
                {
                    ["Varchar"] = new { canConvert = true, isLossy = false, requiresValidation = false },
                    ["Int"] = new { canConvert = true, isLossy = true, requiresValidation = true },
                    ["DateTime"] = new { canConvert = false, isLossy = true, requiresValidation = true },
                    ["Decimal"] = new { canConvert = true, isLossy = false, requiresValidation = false }
                },
                ["DateTime"] = new Dictionary<string, object>
                {
                    ["Varchar"] = new { canConvert = true, isLossy = false, requiresValidation = false },
                    ["Int"] = new { canConvert = false, isLossy = true, requiresValidation = true },
                    ["Decimal"] = new { canConvert = false, isLossy = true, requiresValidation = true },
                    ["DateTime"] = new { canConvert = true, isLossy = false, requiresValidation = false }
                }
            };

            return Ok(new
            {
                conversionMatrix = matrix,
                dataTypes = new[]
                {
                    new { id = 1, name = "Varchar", description = "Metin (NVARCHAR(255))" },
                    new { id = 2, name = "Int", description = "Tamsayı (INT)" },
                    new { id = 3, name = "Decimal", description = "Ondalık sayı (DECIMAL(18,2))" },
                    new { id = 4, name = "DateTime", description = "Tarih ve saat (DATETIME2)" }
                }
            });
        }
    }


    public static class ColumnDataTypeExtensions
    {
        public static ColumnDataType ToColumnDataType(this int value)
        {
            if (!Enum.IsDefined(typeof(ColumnDataType), value))
            {
                throw new ArgumentException($"Invalid ColumnDataType value: {value}");
            }

            return (ColumnDataType)value;
        }

        public static bool IsValidColumnDataType(this int value)
        {
            return Enum.IsDefined(typeof(ColumnDataType), value);
        }
    }

    public class UpdateColumnDataTypeRequest
    {
        
        [Required]
        public int NewDataType { get; set; }

        public bool ForceUpdate { get; set; } = false;

    }

    public class RenameColumnRequest
    {
        [Required]
        [MaxLength(100)]
        public string NewColumnName { get; set; } = string.Empty;
    }
}