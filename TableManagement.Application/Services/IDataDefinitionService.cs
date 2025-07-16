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
    }
}