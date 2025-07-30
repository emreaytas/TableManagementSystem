using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.Services
{
    public interface IExcelService
    {
        /// <summary>
        /// Tablo ID'sine göre Excel dosyası oluşturur ve FileResult döner
        /// </summary>
        /// <param name="tableId">Export edilecek tablo ID'si</param>
        /// <param name="userId">Kullanıcı ID'si (güvenlik için)</param>
        /// <param name="includeHeaders">Başlık satırının dahil edilip edilmeyeceği</param>
        /// <returns>Excel dosyası FileResult</returns>
        Task<FileResult> ExportTableToExcelAsync(int tableId, int userId, bool includeHeaders = true);

        /// <summary>
        /// Tablo ID'sine göre Excel dosyasının byte array'ini döner
        /// </summary>
        /// <param name="tableId">Export edilecek tablo ID'si</param>
        /// <param name="userId">Kullanıcı ID'si (güvenlik için)</param>
        /// <param name="includeHeaders">Başlık satırının dahil edilip edilmeyeceği</param>
        /// <returns>Excel dosyasının byte array'i</returns>
        Task<byte[]> GenerateExcelBytesAsync(int tableId, int userId, bool includeHeaders = true);

        /// <summary>
        /// Tablo ID'sine göre CSV dosyası oluşturur ve FileResult döner
        /// </summary>
        /// <param name="tableId">Export edilecek tablo ID'si</param>
        /// <param name="userId">Kullanıcı ID'si (güvenlik için)</param>
        /// <param name="includeHeaders">Başlık satırının dahil edilip edilmeyeceği</param>
        /// <param name="delimiter">CSV ayırıcı karakter</param>
        /// <returns>CSV dosyası FileResult</returns>
        Task<FileResult> ExportTableToCsvAsync(int tableId, int userId, bool includeHeaders = true, string delimiter = ",");
        
    
    }
}
