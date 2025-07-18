using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.Services;
using TableManagement.Core.DTOs.Requests;

namespace TableManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TablesController : ControllerBase
    {
        private readonly ITableService _tableService;
        private readonly IDataDefinitionService _dataDefinitionService;
        private readonly ILogger<TablesController> _logger;

        public TablesController(
            ITableService tableService,
            IDataDefinitionService dataDefinitionService,
            ILogger<TablesController> logger)
        {
            _tableService = tableService;
            _dataDefinitionService = dataDefinitionService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

      
        [HttpPost("CreateTable")]
        public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Creating table {TableName} for user {UserId}", request.TableName, userId);

                var result = await _tableService.CreateTableAsync(request, userId);

                _logger.LogInformation("Table {TableName} created successfully for user {UserId}", request.TableName, userId);

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = "Tablo başarıyla oluşturuldu."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table creation failed for user {UserId}: {Message}", GetCurrentUserId(), ex.Message);
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablo oluşturulurken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Validasyon ile birlikte yeni tablo oluşturur
        /// </summary>
        [HttpPost("create-with-validation")]
        public async Task<IActionResult> CreateTableWithValidation([FromBody] CreateTableRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Creating table with validation {TableName} for user {UserId}", request.TableName, userId);

                var result = await _tableService.CreateTableWithValidationAsync(request, userId);

                if (!result.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message
                    });
                }

                _logger.LogInformation("Table {TableName} created successfully with validation for user {UserId}", request.TableName, userId);

                return Ok(new
                {
                    success = true,
                    data = result.Table,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table with validation for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablo oluşturulurken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Giriş yapmış kullanıcının tüm tablolarını listeler
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserTables()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Getting tables for user {UserId}", userId);

                var tables = await _tableService.GetUserTablesAsync(userId);

                return Ok(new
                {
                    success = true,
                    data = tables,
                    message = "Tablolar başarıyla getirildi."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tables for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablolar getirilirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Belirtilen tabloyu getirir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTableById(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Getting table {TableId} for user {UserId}", id, userId);

                var table = await _tableService.GetTableByIdAsync(id, userId);

                return Ok(new
                {
                    success = true,
                    data = table,
                    message = "Tablo başarıyla getirildi."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table {TableId} not found for user {UserId}: {Message}", id, GetCurrentUserId(), ex.Message);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablo getirilirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Tabloyu siler
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTable(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Deleting table {TableId} for user {UserId}", id, userId);

                var result = await _tableService.DeleteTableAsync(id, userId);

                if (!result)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Tablo bulunamadı."
                    });
                }

                _logger.LogInformation("Table {TableId} deleted successfully for user {UserId}", id, userId);

                return Ok(new
                {
                    success = true,
                    message = "Tablo başarıyla silindi."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablo silinirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Belirtilen tablonun verilerini getirir
        /// </summary>
        [HttpGet("{id}/data")]
        public async Task<IActionResult> GetTableData(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Getting data for table {TableId} for user {UserId}", id, userId);

                var data = await _tableService.GetTableDataAsync(id, userId);

                return Ok(new
                {
                    success = true,
                    data = data,
                    message = "Tablo verileri başarıyla getirildi."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table {TableId} not found for user {UserId}: {Message}", id, GetCurrentUserId(), ex.Message);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting data for table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablo verileri getirilirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Tabloya yeni veri ekler
        /// </summary>
        [HttpPost("{id}/data")]
        public async Task<IActionResult> AddTableData(int id, [FromBody] AddTableDataRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Ensure the table ID in the request matches the route parameter
            request.TableId = id;

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Adding data to table {TableId} for user {UserId}", id, userId);

                var result = await _tableService.AddTableDataAsync(request, userId);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Veri eklenirken bir hata oluştu."
                    });
                }

                _logger.LogInformation("Data added successfully to table {TableId} for user {UserId}", id, userId);

                return Ok(new
                {
                    success = true,
                    message = "Veri başarıyla eklendi."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table {TableId} not found for user {UserId}: {Message}", id, GetCurrentUserId(), ex.Message);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding data to table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Veri eklenirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Tablo verilerini günceller
        /// </summary>
        [HttpPut("{id}/data/{rowIdentifier}")]
        public async Task<IActionResult> UpdateTableData(int id, int rowIdentifier, [FromBody] Dictionary<int, string> values)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Updating data in table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, userId);

                var result = await _tableService.UpdateTableDataAsync(id, rowIdentifier, values, userId);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Veri güncellenirken bir hata oluştu."
                    });
                }

                _logger.LogInformation("Data updated successfully in table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, userId);

                return Ok(new
                {
                    success = true,
                    message = "Veri başarıyla güncellendi."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table {TableId} not found for user {UserId}: {Message}", id, GetCurrentUserId(), ex.Message);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data in table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Veri güncellenirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Tablo verilerini siler
        /// </summary>
        [HttpDelete("{id}/data/{rowIdentifier}")]
        public async Task<IActionResult> DeleteTableData(int id, int rowIdentifier)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Deleting data from table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, userId);

                var result = await _tableService.DeleteTableDataAsync(id, rowIdentifier, userId);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Veri silinirken bir hata oluştu."
                    });
                }

                _logger.LogInformation("Data deleted successfully from table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, userId);

                return Ok(new
                {
                    success = true,
                    message = "Veri başarıyla silindi."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table {TableId} not found for user {UserId}: {Message}", id, GetCurrentUserId(), ex.Message);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data from table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Veri silinirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Kolon günceller
        /// </summary>
        [HttpPut("{id}/columns")]
        public async Task<IActionResult> UpdateColumn(int id, [FromBody] UpdateColumnRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Updating column {ColumnId} in table {TableId} for user {UserId}", request.ColumnId, id, userId);

                var result = await _tableService.UpdateColumnAsync(id, request, userId);

                if (!result.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message,
                        validationResult = result.ValidationResult
                    });
                }

                _logger.LogInformation("Column {ColumnId} updated successfully in table {TableId} for user {UserId}", request.ColumnId, id, userId);

                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    executedQueries = result.ExecutedQueries,
                    affectedRows = result.AffectedRows
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column {ColumnId} in table {TableId} for user {UserId}", request.ColumnId, id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Kolon güncellenirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Kolon güncelleme validasyonu yapar
        /// </summary>
        [HttpPost("{id}/columns/validate")]
        public async Task<IActionResult> ValidateColumnUpdate(int id, [FromBody] UpdateColumnRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Validating column update for table {TableId} by user {UserId}", id, userId);

                var validationResult = await _tableService.ValidateColumnUpdateAsync(id, request, userId);

                return Ok(new
                {
                    success = true,
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
                _logger.LogError(ex, "Error validating column update for table {TableId} by user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new { message = "Validasyon sırasında hata oluştu." });
            }
        }

        /// <summary>
        /// Tablo güncelleme validasyonu yapar
        /// </summary>
        [HttpPost("{id}/validate-update")]
        public async Task<IActionResult> ValidateTableUpdate(int id, [FromBody] ValidateTableUpdateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Validating table update for table {TableId} by user {UserId}", id, userId);

                var validationResult = await _tableService.ValidateTableUpdateAsync(id, request, userId);

                return Ok(new
                {
                    success = true,
                    isValid = validationResult.IsValid,
                    hasStructuralChanges = validationResult.HasStructuralChanges,
                    hasDataCompatibilityIssues = validationResult.HasDataCompatibilityIssues,
                    requiresForceUpdate = validationResult.RequiresForceUpdate,
                    issues = validationResult.Issues,
                    dataIssues = validationResult.DataIssues,
                    columnIssues = validationResult.ColumnIssues,
                    affectedRowCount = validationResult.AffectedRowCount,
                    estimatedBackupSize = validationResult.EstimatedBackupSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating table update for table {TableId} by user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new { message = "Validasyon sırasında hata oluştu." });
            }
        }

        /// <summary>
        /// Tabloyu günceller
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTable(int id, [FromBody] UpdateTableRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Updating table {TableId} by user {UserId}", id, userId);

                // First validate the update
                var validateRequest = new ValidateTableUpdateRequest
                {
                    TableId = request.TableId,
                    TableName = request.TableName,
                    Description = request.Description,
                    Columns = request.Columns
                };

                var validationResult = await _tableService.ValidateTableUpdateAsync(id, validateRequest, userId);

                if (!validationResult.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tablo güncellemesi geçersiz",
                        issues = validationResult.Issues,
                        dataIssues = validationResult.DataIssues,
                        columnIssues = validationResult.ColumnIssues
                    });
                }

                if (validationResult.RequiresForceUpdate && !request.Columns?.Any(c => c.ForceUpdate) == true)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Bu güncelleme veri kaybına neden olabilir. Zorla güncelleme gerekli.",
                        requiresForceUpdate = true,
                        validationResult = validationResult
                    });
                }

                // Perform the update
                var updateResult = await _tableService.UpdateTableAsync(id, request, userId);

                if (!updateResult.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = updateResult.Message
                    });
                }

                _logger.LogInformation("Table {TableId} updated successfully by user {UserId}", id, userId);

                return Ok(new
                {
                    success = true,
                    message = "Tablo başarıyla güncellendi",
                    table = updateResult.Table,
                    executedQueries = updateResult.ExecutedQueries,
                    affectedRows = updateResult.AffectedRows,
                    backupCreated = updateResult.BackupCreated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating table {TableId} by user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablo güncellenirken hata oluştu."
                });
            }
        }
    }
}