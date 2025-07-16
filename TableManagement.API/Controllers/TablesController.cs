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

        /// <summary>
        /// Kullanıcı için yeni tablo oluşturur (metadata + DDL)
        /// </summary>
        [HttpPost]
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
                    message = "Tablo başarıyla oluşturuldu",
                    table = result
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table creation failed for user {UserId}: {Message}", GetCurrentUserId(), ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new { message = "Tablo oluşturulurken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Kullanıcının tüm tablolarını listeler
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserTables()
        {
            try
            {
                var userId = GetCurrentUserId();
                var tables = await _tableService.GetUserTablesAsync(userId);
                return Ok(tables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tables for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new { message = "Tablolar listelenirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Belirtilen ID'ye sahip tablo bilgilerini getirir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTable(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var table = await _tableService.GetTableByIdAsync(id, userId);

                if (table == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                return Ok(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new { message = "Tablo getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Belirtilen tablonun verilerini getirir (gerçek veritabanından)
        /// </summary>
        [HttpGet("{id}/data")]
        public async Task<IActionResult> GetTableData(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _tableService.GetTableDataAsync(id, userId);

                if (data == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table data for table {TableId} and user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new { message = "Tablo verileri getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Tabloyu siler (metadata + DDL)
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
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                _logger.LogInformation("Table {TableId} deleted successfully for user {UserId}", id, userId);
                return Ok(new { message = "Tablo başarıyla silindi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new { message = "Tablo silinirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Tablonun gerçek veritabanı adını getirir (debug amaçlı)
        /// </summary>
        [HttpGet("{id}/info")]
        public async Task<IActionResult> GetTableInfo(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var table = await _tableService.GetTableByIdAsync(id, userId);

                if (table == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                var actualTableName = _dataDefinitionService.GenerateSecureTableName(table.TableName, userId);

                return Ok(new
                {
                    tableId = table.Id,
                    userTableName = table.TableName,
                    actualDatabaseTableName = actualTableName,
                    columns = table.Columns,
                    createdAt = table.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table info for table {TableId} and user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new { message = "Tablo bilgileri getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Tablonun SQL şemasını getirir (debug amaçlı)
        /// </summary>
        [HttpGet("{id}/schema")]
        public async Task<IActionResult> GetTableSchema(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var table = await _tableService.GetTableByIdAsync(id, userId);

                if (table == null)
                {
                    return NotFound(new { message = "Tablo bulunamadı." });
                }

                var schema = new
                {
                    tableName = _dataDefinitionService.GenerateSecureTableName(table.TableName, userId),
                    columns = table.Columns.Select(c => new
                    {
                        name = c.ColumnName,
                        dataType = c.DataType,
                        sqlType = _dataDefinitionService.ConvertToSqlDataType(c.DataType),
                        isRequired = c.IsRequired,
                        defaultValue = c.DefaultValue,
                        displayOrder = c.DisplayOrder
                    }).OrderBy(c => c.displayOrder)
                };

                return Ok(schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table schema for table {TableId} and user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new { message = "Tablo şeması getirilirken bir hata oluştu." });
            }
        }
    }
}