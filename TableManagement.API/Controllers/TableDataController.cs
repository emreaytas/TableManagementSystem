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
    public class TableDataController : ControllerBase
    {
        private readonly ITableService _tableService;

        public TableDataController(ITableService tableService)
        {
            _tableService = tableService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet("{tableId}")]
        public async Task<IActionResult> GetTableData(int tableId)
        {
            var userId = GetCurrentUserId();
            var data = await _tableService.GetTableDataAsync(tableId, userId);

            if (data == null)
            {
                return NotFound(new { message = "Tablo bulunamadı." });
            }

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> AddTableData([FromBody] AddTableDataRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var result = await _tableService.AddTableDataAsync(request, userId);

            if (!result)
            {
                return BadRequest(new { message = "Veri eklenirken bir hata oluştu." });
            }

            return Ok(new { message = "Veri başarıyla eklendi." });
        }

        [HttpPut("{tableId}/rows/{rowIdentifier}")]
        public async Task<IActionResult> UpdateTableData(int tableId, int rowIdentifier, [FromBody] Dictionary<int, string> values)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var result = await _tableService.UpdateTableDataAsync(tableId, rowIdentifier, values, userId);

            if (!result)
            {
                return BadRequest(new { message = "Veri güncellenirken bir hata oluştu." });
            }

            return Ok(new { message = "Veri başarıyla güncellendi." });
        }

        [HttpDelete("{tableId}/rows/{rowIdentifier}")]
        public async Task<IActionResult> DeleteTableData(int tableId, int rowIdentifier)
        {
            var userId = GetCurrentUserId();
            var result = await _tableService.DeleteTableDataAsync(tableId, rowIdentifier, userId);

            if (!result)
            {
                return BadRequest(new { message = "Veri silinirken bir hata oluştu." });
            }

            return Ok(new { message = "Veri başarıyla silindi." });
        }
    }
}