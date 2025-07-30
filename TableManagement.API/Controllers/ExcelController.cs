// TableManagement.API/Controllers/ExcelController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TableManagement.Application.Services;

namespace TableManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExcelController : ControllerBase
    {
        private readonly IExcelService _excelService;
        private readonly ILogger<ExcelController> _logger;

        public ExcelController(IExcelService excelService, ILogger<ExcelController> logger)
        {
            _excelService = excelService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        /// <summary>
        /// Excel dosyası indir
        /// </summary>
        [HttpGet("download/{tableId}")]
        public async Task<IActionResult> DownloadExcel(int tableId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var fileResult = await _excelService.ExportTableToExcelAsync(tableId, userId);
                return fileResult;
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel export hatası - Tablo ID: {TableId}", tableId);
                return StatusCode(500, new { success = false, message = "Excel oluşturma hatası" });
            }
        }

        /// <summary>
        /// CSV dosyası indir
        /// </summary>
        [HttpGet("download-csv/{tableId}")]
        public async Task<IActionResult> DownloadCsv(int tableId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var fileResult = await _excelService.ExportTableToCsvAsync(tableId, userId);
                return fileResult;
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV export hatası - Tablo ID: {TableId}", tableId);
                return StatusCode(500, new { success = false, message = "CSV oluşturma hatası" });
            }
        }
    }
}