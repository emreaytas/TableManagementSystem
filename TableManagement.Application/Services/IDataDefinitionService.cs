using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.Entities;
using TableManagement.Core.Enums;

namespace TableManagement.Application.Services
{
    public interface IDataDefinitionService
    {
        /// <summary>
        /// Kullanıcı için dinamik tablo oluşturur
        /// </summary>
        Task<bool> CreateUserTableAsync(string tableName, List<CustomColumn> columns, int userId);

        /// <summary>
        /// Kullanıcının tablosunu siler
        /// </summary>
        Task<bool> DropUserTableAsync(string tableName, int userId);

        /// <summary>
        /// Kullanıcı tablosuna veri ekler
        /// </summary>
        Task<bool> InsertDataToUserTableAsync(string tableName, Dictionary<string, object> data, int userId);

        /// <summary>
        /// Kullanıcı tablosundan veri okur
        /// </summary>
        Task<List<Dictionary<string, object>>> SelectDataFromUserTableAsync(string tableName, int userId);

        /// <summary>
        /// Kullanıcı tablosunda veri günceller
        /// </summary>
        Task<bool> UpdateDataInUserTableAsync(string tableName, Dictionary<string, object> data, string whereClause, int userId);

        /// <summary>
        /// Kullanıcı tablosundan veri siler
        /// </summary>
        Task<bool> DeleteDataFromUserTableAsync(string tableName, string whereClause, int userId);

        /// <summary>
        /// Güvenli tablo adı oluşturur
        /// </summary>
        string GenerateSecureTableName(string tableName, int userId);

        /// <summary>
        /// Veri tipini SQL karşılığına çevirir
        /// </summary>
        string ConvertToSqlDataType(ColumnDataType dataType);

        /// <summary>
        /// Kolon veri tipini değiştirir
        /// </summary>
        Task<ColumnUpdateResult> UpdateColumnDataTypeAsync(string tableName, string columnName, ColumnDataType newDataType, bool forceUpdate, int userId);

        /// <summary>
        /// Kolon güncelleme işleminden önce veri uyumluluğunu kontrol eder
        /// </summary>
        Task<ColumnValidationResult> ValidateColumnUpdateAsync(string tableName, string columnName, ColumnDataType newDataType, int userId);

        /// <summary>
        /// Kolonu yeniden adlandırır
        /// </summary>
        Task<bool> RenameColumnAsync(string tableName, string oldColumnName, string newColumnName, int userId);

        /// <summary>
        /// Kolon varsayılan değerini günceller
        /// </summary>
        Task<bool> UpdateColumnDefaultValueAsync(string tableName, string columnName, string defaultValue, int userId);

        /// <summary>
        /// Kolon NULL/NOT NULL durumunu günceller
        /// </summary>
        Task<bool> UpdateColumnNullabilityAsync(string tableName, string columnName, bool isRequired, int userId);
    }
}