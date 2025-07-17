using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.Services;

namespace TableManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public partial class TablesController : ControllerBase
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

        /// <summary>
        /// Validates table update before actual update
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
        /// Updates table structure and data
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
                        message = "Bu güncelleme veri kaybına neden olabilir. Zorla güncelleme gerekli.",
                        requiresForceUpdate = true,
                        validationResult = validationResult
                    });
                }

                // Perform the update
                var updateResult = await _tableService.UpdateTableAsync(id, request, userId);

                if (!updateResult.Success)
                {
                    return BadRequest(new { message = updateResult.Message });
                }

                _logger.LogInformation("Table {TableId} updated successfully by user {UserId}", id, userId);

                return Ok(new
                {
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
                return StatusCode(500, new { message = "Tablo güncellenirken hata oluştu." });
            }
        }
    }
}