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
    [Authorize] // istenen kullanıcının giriş yapmış olması gerekiyor ve bilgileri gerekiyor böylece ilerleyebiliyoruz.
    public class TableDataController : ControllerBase
    {
        private readonly ITableService _tableService;
        private readonly IDataDefinitionService _dataDefinitionService;
        private readonly ILogger<TableDataController> _logger;

        public TableDataController(
            ITableService tableService,
            IDataDefinitionService dataDefinitionService,
            ILogger<TableDataController> logger)
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
        /// Belirtilen tablonun verilerini getirir (gerçek veritabanından)
        /// </summary>
        [HttpGet("{tableId}")]
        public async Task<IActionResult> GetTableData(int tableId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _tableService.GetTableDataAsync(tableId, userId);

                if (data == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table data for table {TableId} and user {UserId}", tableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Tablo verileri getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Tabloya yeni veri ekler (gerçek veritabanına)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddTableData([FromBody] AddTableDataRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Adding data to table {TableId} for user {UserId}", request.TableId, userId);

                var result = await _tableService.AddTableDataAsync(request, userId);

                if (!result)
                {
                    return BadRequest(new { message = "Veri eklenirken bir hata oluştu." });
                }

                _logger.LogInformation("Data added successfully to table {TableId} for user {UserId}", request.TableId, userId);
                return Ok(new { message = "Veri başarıyla eklendi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding data to table {TableId} for user {UserId}", request.TableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Veri eklenirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Tablodaki veriyi günceller (gerçek veritabanında)
        /// </summary>
        [HttpPut("{tableId}/rows/{rowIdentifier}")]
        public async Task<IActionResult> UpdateTableData(int tableId, int rowIdentifier, [FromBody] Dictionary<int, string> values)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Updating data in table {TableId} row {RowId} for user {UserId}", tableId, rowIdentifier, userId);

                var result = await _tableService.UpdateTableDataAsync(tableId, rowIdentifier, values, userId);

                if (!result)
                {
                    return BadRequest(new { message = "Veri güncellenirken bir hata oluştu." });
                }

                _logger.LogInformation("Data updated successfully in table {TableId} row {RowId} for user {UserId}", tableId, rowIdentifier, userId);
                return Ok(new { message = "Veri başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data in table {TableId} row {RowId} for user {UserId}", tableId, rowIdentifier, GetCurrentUserId());
                return StatusCode(500, new { message = "Veri güncellenirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Tablodan veri siler (gerçek veritabanından)
        /// </summary>
        [HttpDelete("{tableId}/rows/{rowIdentifier}")]
        public async Task<IActionResult> DeleteTableData(int tableId, int rowIdentifier)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Deleting data from table {TableId} row {RowId} for user {UserId}", tableId, rowIdentifier, userId);

                var result = await _tableService.DeleteTableDataAsync(tableId, rowIdentifier, userId);

                if (!result)
                {
                    return BadRequest(new { message = "Veri silinirken bir hata oluştu." });
                }

                _logger.LogInformation("Data deleted successfully from table {TableId} row {RowId} for user {UserId}", tableId, rowIdentifier, userId);
                return Ok(new { message = "Veri başarıyla silindi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data from table {TableId} row {RowId} for user {UserId}", tableId, rowIdentifier, GetCurrentUserId());
                return StatusCode(500, new { message = "Veri silinirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Tablonun raw SQL verilerini getirir (debug amaçlı)
        /// </summary>
        [HttpGet("{tableId}/raw")]
        public async Task<IActionResult> GetRawTableData(int tableId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var table = await _tableService.GetTableByIdAsync(tableId, userId);

                if (table == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                var rawData = await _dataDefinitionService.SelectDataFromUserTableAsync(table.TableName, userId);

                return Ok(new
                {
                    tableName = table.TableName,
                    actualTableName = _dataDefinitionService.GenerateSecureTableName(table.TableName, userId),
                    rowCount = rawData.Count,
                    data = rawData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving raw table data for table {TableId} and user {UserId}", tableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Raw tablo verileri getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Ham SQL sorgusu çalıştırır (debug amaçlı - dikkatli kullanın)
        /// </summary>
        [HttpPost("{tableId}/query")]
        public async Task<IActionResult> ExecuteRawQuery(int tableId, [FromBody] RawQueryRequest request)
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

                // Güvenlik kontrolü - sadece SELECT sorgularına izin ver
                if (!request.Query.Trim().ToUpperInvariant().StartsWith("SELECT"))
                {
                    return BadRequest(new { message = "Sadece SELECT sorguları desteklenir." });
                }

                var actualTableName = _dataDefinitionService.GenerateSecureTableName(table.TableName, userId);
                var modifiedQuery = request.Query.Replace("{TABLE_NAME}", actualTableName);

                // Bu örnekte basit bir implementasyon var - production'da daha güvenli olmalı
                _logger.LogWarning("Raw query executed by user {UserId} on table {TableId}: {Query}", userId, tableId, modifiedQuery);

                return Ok(new
                {
                    message = "Raw query özelliği henüz implementasyonda değil",
                    actualTableName = actualTableName,
                    modifiedQuery = modifiedQuery
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing raw query for table {TableId} and user {UserId}", tableId, GetCurrentUserId());
                return StatusCode(500, new { message = "Sorgu çalıştırılırken bir hata oluştu." });
            }
        }
    }

    public class RawQueryRequest
    {
        public string Query { get; set; } = string.Empty;
    }
}