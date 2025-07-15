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

        public TablesController(ITableService tableService)
        {
            _tableService = tableService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

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
                var result = await _tableService.CreateTableAsync(request, userId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Tablo oluşturulurken bir hata oluştu." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserTables()
        {
            var userId = GetCurrentUserId();
            var tables = await _tableService.GetUserTablesAsync(userId);
            return Ok(tables);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTable(int id)
        {
            var userId = GetCurrentUserId();
            var table = await _tableService.GetTableByIdAsync(id, userId);

            if (table == null)
            {
                return NotFound(new { message = "Tablo bulunamadı." });
            }

            return Ok(table);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTable(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _tableService.DeleteTableAsync(id, userId);

            if (!result)
            {
                return NotFound(new { message = "Tablo bulunamadı." });
            }

            return Ok(new { message = "Tablo başarıyla silindi." });
        }
    }
}