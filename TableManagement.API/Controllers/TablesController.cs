using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.Services;
using TableManagement.Core.DTOs.Requests;
using TableManagement.Core.Entities;
using TableManagement.Core.Enums;
using TableManagement.Core.Interfaces;

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
        private readonly IUnitOfWork _unitOfWork;
        public TablesController(
            ITableService tableService,
            IDataDefinitionService dataDefinitionService,
            ILogger<TablesController> logger,IUnitOfWork unitOfWork)
        {
            _tableService = tableService;
            _dataDefinitionService = dataDefinitionService;
            _logger = logger;
            _unitOfWork = unitOfWork;   
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }


        // TablesController.cs içine eklenecek metodlar

        /// <summary>
        /// Fiziksel tablo durumunu debug eder
        /// </summary>
        [HttpGet("{id}/debug-physical-table")]
        public async Task<IActionResult> DebugPhysicalTable(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("=== DEBUG PHYSICAL TABLE FOR ID: {TableId} ===", id);

                // 1. CustomTables'tan bilgiyi al
                var customTable = await _unitOfWork.CustomTables.GetUserTableByIdAsync(id, userId);

                // 2. Fiziksel tabloları al
                var allUserTables = await _dataDefinitionService.GetAllUserTablesAsync(userId);
                var allSystemTables = await _dataDefinitionService.GetAllTablesDebugAsync();

                // 3. Olası tablo isimlerini oluştur
                var possibleNames = new List<string>();
                if (customTable != null)
                {
                    possibleNames.Add(_dataDefinitionService.GenerateSecureTableName(customTable.TableName, userId));
                    possibleNames.Add($"Table_{userId}_{customTable.TableName}");
                    possibleNames.Add($"Table_{userId}_{customTable.TableName.Replace(" ", "_")}");

                    // Türkçe karakterler normalize
                    var normalized = customTable.TableName
                        .Replace("ş", "s").Replace("Ş", "S")
                        .Replace("ç", "c").Replace("Ç", "C")
                        .Replace("ı", "i").Replace("İ", "I")
                        .Replace("ğ", "g").Replace("Ğ", "G")
                        .Replace("ü", "u").Replace("Ü", "U")
                        .Replace("ö", "o").Replace("Ö", "O")
                        .Replace(" ", "_");
                    possibleNames.Add($"Table_{userId}_{normalized}");
                }

                return Ok(new
                {
                    tableId = id,
                    userId = userId,
                    customTableInfo = customTable != null ? new
                    {
                        id = customTable.Id,
                        tableName = customTable.TableName,
                        description = customTable.Description,
                        createdAt = customTable.CreatedAt,
                        columnCount = customTable.Columns?.Count ?? 0,
                        columns = customTable.Columns?.Select(c => new {
                            c.Id,
                            c.ColumnName,
                            c.DataType,
                            c.IsRequired,
                            c.DisplayOrder
                        }).ToList()
                    } : null,
                    possiblePhysicalNames = possibleNames,
                    actualUserTables = allUserTables,
                    totalSystemTables = allSystemTables.Count,
                    firstFewSystemTables = allSystemTables.Take(20).ToList(),
                    userTablesContainingUserId = allUserTables.Where(t => t.Contains($"_{userId}_")).ToList(),
                    anyTableContainingTest = allSystemTables.Where(t =>
                        t.ToLower().Contains("test") ||
                        t.ToLower().Contains("tablosu") ||
                        t.Contains($"Table_{userId}")).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in debug endpoint for table {TableId}", id);
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Fiziksel tabloyu yeniden oluşturur
        /// </summary>
        [HttpPost("{id}/recreate-physical-table")]
        public async Task<IActionResult> RecreatePhysicalTable(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Attempting to recreate physical table for CustomTable ID: {TableId}", id);

                var customTable = await _unitOfWork.CustomTables.GetUserTableWithColumnsAsync(id, userId);

                if (customTable == null)
                {
                    return NotFound(new
                    {
                        error = "CustomTable bulunamadı",
                        tableId = id,
                        userId = userId
                    });
                }

                // Fiziksel tablo adını oluştur
                var physicalTableName = _dataDefinitionService.GenerateSecureTableName(customTable.TableName, userId);
                _logger.LogInformation("Generated physical table name: {PhysicalTableName}", physicalTableName);

                // Önce var mı kontrol et
                var exists = await _dataDefinitionService.TableExistsAsync(physicalTableName);
                if (exists)
                {
                    return BadRequest(new
                    {
                        error = $"Fiziksel tablo zaten var: {physicalTableName}",
                        physicalTableName = physicalTableName,
                        suggestion = "Önce tabloyu silin veya farklı bir isim kullanın"
                    });
                }

                // Kolonları kontrol et
                if (customTable.Columns == null || !customTable.Columns.Any())
                {
                    return BadRequest(new
                    {
                        error = "Tablo kolonları bulunamadı",
                        tableId = id,
                        tableName = customTable.TableName
                    });
                }

                _logger.LogInformation("Creating physical table with {ColumnCount} columns", customTable.Columns.Count);

                // Mevcut CreateUserTableAsync metodunu kullan
                var result = await _dataDefinitionService.CreateUserTableAsync(
                    customTable.TableName,
                    customTable.Columns.ToList(),
                    userId);

                if (result)
                {
                    _logger.LogInformation("Physical table created successfully: {PhysicalTableName}", physicalTableName);
                }
                else
                {
                    _logger.LogError("Failed to create physical table: {PhysicalTableName}", physicalTableName);
                }

                return Ok(new
                {
                    success = result,
                    physicalTableName = physicalTableName,
                    customTableId = id,
                    customTableName = customTable.TableName,
                    columnCount = customTable.Columns.Count,
                    columns = customTable.Columns.Select(c => new {
                        c.ColumnName,
                        c.DataType,
                        c.IsRequired,
                        c.DefaultValue
                    }).ToList(),
                    message = result ?
                        "Fiziksel tablo başarıyla oluşturuldu. Şimdi güncelleme işlemini deneyebilirsiniz." :
                        "Fiziksel tablo oluşturulamadı. Loglara bakın."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recreating physical table for {TableId}", id);
                return StatusCode(500, new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    tableId = id
                });
            }
        }

        /// <summary>
        /// Fiziksel tabloyu siler (temizlik için)
        /// </summary>
        [HttpDelete("{id}/delete-physical-table")]
        public async Task<IActionResult> DeletePhysicalTable(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var customTable = await _unitOfWork.CustomTables.GetUserTableByIdAsync(id, userId);

                if (customTable == null)
                {
                    return NotFound("CustomTable bulunamadı");
                }

                // Fiziksel tabloyu sil (DropUserTableAsync kullan)
                var result = await _dataDefinitionService.DropUserTableAsync(customTable.TableName, userId);

                return Ok(new
                {
                    success = result,
                    message = result ? "Fiziksel tablo silindi" : "Fiziksel tablo silinemedi",
                    tableName = customTable.TableName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting physical table for {TableId}", id);
                return StatusCode(500, new { error = ex.Message });
            }
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

        // TableManagement.API/Controllers/TablesController.cs
        // UpdateTable metodunu tam haliyle güncelleyin:

        /// <summary>
        /// Tabloyu günceller - Akıllı veri kontrolü ile
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
                _logger.LogInformation("🔄 Starting table update for {TableId} by user {UserId}", id, userId);

                // 🔥 ADIM 1: Validasyon kontrolü yap
                var validateRequest = new ValidateTableUpdateRequest
                {
                    TableId = request.TableId,
                    TableName = request.TableName,
                    Description = request.Description,
                    Columns = request.Columns
                };

                var validationResult = await _tableService.ValidateTableUpdateAsync(id, validateRequest, userId);

                _logger.LogInformation("📋 Validation completed - IsValid: {IsValid}, RequiresForce: {RequiresForce}, HasDataIssues: {HasDataIssues}",
                    validationResult.IsValid, validationResult.RequiresForceUpdate, validationResult.HasDataCompatibilityIssues);

                // 🔥 ADIM 2: Geçersiz validasyon
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("❌ Validation failed for table {TableId}: {Issues}", id, string.Join(", ", validationResult.Issues));
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tablo güncellemesi geçersiz",
                        issues = validationResult.Issues,
                        dataIssues = validationResult.DataIssues,
                        columnIssues = validationResult.ColumnIssues,
                        validationResult = validationResult
                    });
                }

                // 🔥 ADIM 3: Force update gerekli mi kontrol et
                var requiresForceUpdate = validationResult.RequiresForceUpdate;
                var hasForceUpdatePermission = request.Columns?.Any(c => c.ForceUpdate == true) == true;

                if (requiresForceUpdate && !hasForceUpdatePermission)
                {
                    _logger.LogWarning("⚠️ Force update required for table {TableId} but not provided", id);

                    // Detaylı mesaj hazırla
                    var forceUpdateReasons = new List<string>();

                    if (validationResult.ColumnIssues?.Any() == true)
                    {
                        foreach (var issue in validationResult.ColumnIssues)
                        {
                            if (issue.Value?.Any(v => !v.StartsWith("✅") && !v.StartsWith("ℹ️")) == true)
                            {
                                forceUpdateReasons.AddRange(issue.Value.Where(v => !v.StartsWith("✅") && !v.StartsWith("ℹ️")));
                            }
                        }
                    }

                    if (validationResult.DataIssues?.Any() == true)
                    {
                        forceUpdateReasons.AddRange(validationResult.DataIssues);
                    }

                    return BadRequest(new
                    {
                        success = false,
                        message = "Bu güncelleme veri kaybına neden olabilir. Zorla güncelleme gerekli.",
                        requiresForceUpdate = true,
                        forceUpdateReasons = forceUpdateReasons,
                        columnIssues = validationResult.ColumnIssues,
                        dataIssues = validationResult.DataIssues,
                        validationResult = validationResult
                    });
                }

                // 🔥 ADIM 4: Güncellemeyi gerçekleştir
                _logger.LogInformation("✅ Proceeding with table update for {TableId}", id);

                var updateResult = await _tableService.UpdateTableAsync(id, request, userId);

                if (!updateResult.Success)
                {
                    _logger.LogError("❌ Table update failed for {TableId}: {Message}", id, updateResult.Message);
                    return BadRequest(new
                    {
                        success = false,
                        message = updateResult.Message,
                        validationResult = updateResult.ValidationResult
                    });
                }

                // 🔥 ADIM 5: Başarılı yanıt
                _logger.LogInformation("🎉 Table {TableId} updated successfully by user {UserId}. Affected rows: {AffectedRows}",
                    id, userId, updateResult.AffectedRows);

                // Güvenli değişiklikleri ve uyarıları ayır
                var safeChanges = new List<string>();
                var warningChanges = new List<string>();

                if (validationResult.ColumnIssues?.Any() == true)
                {
                    foreach (var columnIssue in validationResult.ColumnIssues)
                    {
                        foreach (var issue in columnIssue.Value ?? new List<string>())
                        {
                            if (issue.StartsWith("✅") || issue.StartsWith("ℹ️"))
                            {
                                safeChanges.Add($"{columnIssue.Key}: {issue}");
                            }
                            else if (issue.StartsWith("⚠️"))
                            {
                                warningChanges.Add($"{columnIssue.Key}: {issue}");
                            }
                        }
                    }
                }

                var response = new
                {
                    success = true,
                    message = updateResult.Message,
                    table = updateResult.Table,
                    executedQueries = updateResult.ExecutedQueries,
                    affectedRows = updateResult.AffectedRows,
                    validationResult = new
                    {
                        safeChanges = safeChanges,
                        warningChanges = warningChanges,
                        columnIssues = validationResult.ColumnIssues,
                        hasStructuralChanges = validationResult.HasStructuralChanges,
                        hasDataCompatibilityIssues = validationResult.HasDataCompatibilityIssues
                    }
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("🔍 Table {TableId} not found for user {UserId}: {Message}", id, GetCurrentUserId(), ex.Message);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error updating table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Tablo güncellenirken beklenmeyen bir hata oluştu.",
                    details = ex.Message
                });
            }
        }


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

 
        [HttpDelete("{id}/data/{rowId}")]
        public async Task<IActionResult> DeleteTableData(int id, int rowId)
        {
            try
            {
                var userId = GetCurrentUserId();
   

                var result = await _tableService.DeleteTableDataAsync(id, rowId, userId);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Veri silinirken bir hata oluştu."
                    });
                }


                return Ok(new
                {
                    success = true,
                    message = "Veri başarıyla silindi."
                });
            }
            catch (ArgumentException ex)
            {
        
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
          
                return StatusCode(500, new
                {
                    success = false,
                    message = "Veri silinirken bir hata oluştu."
                });
            }
        }





















































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
        /// Tablo verilerini günceller (Column Name bazlı - YENİ)
        /// </summary>
        [HttpPut("{id}/data/{rowId}")]
        public async Task<IActionResult> UpdateTableData(int id, int rowId, [FromBody] Dictionary<string, string> columnValues)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var request = new UpdateTableDataRequest
            {
                TableId = id,
                RowId = rowId,
                ColumnValues = columnValues
            };

            try
            {
                var userId = GetCurrentUserId();

                var result = await _tableService.UpdateTableDataAsync(request, userId);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Veri güncellenirken bir hata oluştu."
                    });
                }

       

                return Ok(new
                {
                    success = true,
                    message = "Veri başarıyla güncellendi."
                });
            }
            catch (ArgumentException ex)
            {
 
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
 
                return StatusCode(500, new
                {
                    success = false,
                    message = "Veri güncellenirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Tabloya yeni veri ekler (Column ID bazlı - ESKİ, backward compatibility)
        /// </summary>
        [HttpPost("{id}/data-by-id")]
        public async Task<IActionResult> AddTableDataById(int id, [FromBody] AddTableDataByIdRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            request.TableId = id;

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Adding data by ID to table {TableId} for user {UserId}", id, userId);

                var result = await _tableService.AddTableDataByIdAsync(request, userId);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Veri eklenirken bir hata oluştu."
                    });
                }

                _logger.LogInformation("Data added successfully by ID to table {TableId} for user {UserId}", id, userId);

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
                _logger.LogError(ex, "Error adding data by ID to table {TableId} for user {UserId}", id, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Veri eklenirken bir hata oluştu."
                });
            }
        }

        /// <summary>
        /// Tablo verilerini günceller (Column ID bazlı - ESKİ, backward compatibility)
        /// </summary>
        [HttpPut("{id}/data-by-id/{rowIdentifier}")]
        public async Task<IActionResult> UpdateTableDataById(int id, int rowIdentifier, [FromBody] Dictionary<int, string> values)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Updating data by ID in table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, userId);

                var result = await _tableService.UpdateTableDataByIdAsync(id, rowIdentifier, values, userId);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Veri güncellenirken bir hata oluştu."
                    });
                }

                _logger.LogInformation("Data updated successfully by ID in table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, userId);

                return Ok(new
                {
                    success = true,
                    message = "Veri başarıyla güncellendi."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Table {TableId} or row {RowIdentifier} not found for user {UserId}: {Message}", id, rowIdentifier, GetCurrentUserId(), ex.Message);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data by ID in table {TableId}, row {RowIdentifier} for user {UserId}", id, rowIdentifier, GetCurrentUserId());
                return StatusCode(500, new
                {
                    success = false,
                    message = "Veri güncellenirken bir hata oluştu."
                });
            }
        }


    }
}