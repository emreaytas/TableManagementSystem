using FluentValidation;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Core.Enums;
using System.Text.RegularExpressions;

namespace TableManagement.Application.Validators
{
    public class CreateTableRequestValidator : AbstractValidator<CreateTableRequest>
    {
        private readonly Regex _safeNamePattern = new Regex(@"^[a-zA-ZığüşöçİĞÜŞÖÇ][a-zA-ZığüşöçİĞÜŞÖÇ0-9_]*$");

        // SQL reserved words listesi
        private readonly HashSet<string> _sqlReservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER", "TABLE",
            "INDEX", "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER", "DATABASE", "SCHEMA", "PRIMARY", "KEY",
            "FOREIGN", "UNIQUE", "NULL", "NOT", "AND", "OR", "IN", "EXISTS", "BETWEEN", "LIKE", "IS",
            "AS", "ON", "INNER", "LEFT", "RIGHT", "FULL", "JOIN", "UNION", "GROUP", "ORDER", "HAVING",
            "DISTINCT", "TOP", "LIMIT", "OFFSET", "CASE", "WHEN", "THEN", "ELSE", "END", "IF", "WHILE",
            "FOR", "DECLARE", "SET", "EXEC", "EXECUTE", "RETURN", "BEGIN", "COMMIT", "ROLLBACK", "TRANSACTION",
            "GRANT", "REVOKE", "DENY", "USER", "ROLE", "PERMISSION", "CAST", "CONVERT", "SUBSTRING", "LEN",
            "UPPER", "LOWER", "TRIM", "REPLACE", "DATEADD", "DATEDIFF", "GETDATE", "YEAR", "MONTH", "DAY"
        };

        public CreateTableRequestValidator()
        {
            RuleFor(x => x.TableName)
                .NotEmpty().WithMessage("Tablo adı boş olamaz.")
                .MaximumLength(50).WithMessage("Tablo adı 50 karakterden fazla olamaz.")
                .Must(BeValidSqlIdentifier).WithMessage("Tablo adı geçerli bir SQL tanımlayıcısı olmalıdır. Sadece harf, rakam ve alt çizgi kullanabilirsiniz.")
                .Must(NotBeSqlReservedWord).WithMessage("Tablo adı SQL ayrılmış kelimesi olamaz.")
                .Must(StartWithLetter).WithMessage("Tablo adı harf ile başlamalıdır.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Açıklama 500 karakterden fazla olamaz.");

            RuleFor(x => x.Columns)
                .NotEmpty().WithMessage("En az bir sütun tanımlanmalıdır.")
                .Must(columns => columns?.Count >= 1).WithMessage("En az bir sütun tanımlanmalıdır.")
                .Must(columns => columns?.Count <= 50).WithMessage("En fazla 50 sütun tanımlanabilir.");

            RuleForEach(x => x.Columns).SetValidator(new CreateColumnRequestValidator());

            // Sütun adlarının benzersiz olması kontrolü
            RuleFor(x => x.Columns)
                .Must(columns => columns?.Select(c => c.ColumnName?.ToLower().Trim()).Distinct().Count() == columns?.Count)
                .WithMessage("Sütun adları benzersiz olmalıdır.")
                .When(x => x.Columns != null && x.Columns.Any());

            // Display order kontrolü
            RuleFor(x => x.Columns)
                .Must(HaveUniqueDisplayOrders)
                .WithMessage("Sütun sıralama değerleri benzersiz olmalıdır.")
                .When(x => x.Columns != null && x.Columns.Any());
        }

        private bool BeValidSqlIdentifier(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return _safeNamePattern.IsMatch(name);
        }

        private bool NotBeSqlReservedWord(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return !_sqlReservedWords.Contains(name);
        }

        private bool StartWithLetter(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return char.IsLetter(name[0]);
        }

        private bool HaveUniqueDisplayOrders(List<CreateColumnRequest>? columns)
        {
            if (columns == null || !columns.Any())
                return true;

            var displayOrders = columns.Select(c => c.DisplayOrder).ToList();
            return displayOrders.Distinct().Count() == displayOrders.Count;
        }
    }

    public class CreateColumnRequestValidator : AbstractValidator<CreateColumnRequest>
    {
        private readonly Regex _safeNamePattern = new Regex(@"^[a-zA-ZığüşöçİĞÜŞÖÇ][a-zA-ZığüşöçİĞÜŞÖÇ0-9_]*$");

        // SQL reserved words listesi
        private readonly HashSet<string> _sqlReservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER", "TABLE",
            "INDEX", "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER", "DATABASE", "SCHEMA", "PRIMARY", "KEY",
            "FOREIGN", "UNIQUE", "NULL", "NOT", "AND", "OR", "IN", "EXISTS", "BETWEEN", "LIKE", "IS",
            "AS", "ON", "INNER", "LEFT", "RIGHT", "FULL", "JOIN", "UNION", "GROUP", "ORDER", "HAVING",
            "DISTINCT", "TOP", "LIMIT", "OFFSET", "CASE", "WHEN", "THEN", "ELSE", "END", "IF", "WHILE",
            "FOR", "DECLARE", "SET", "EXEC", "EXECUTE", "RETURN", "BEGIN", "COMMIT", "ROLLBACK", "TRANSACTION",
            "GRANT", "REVOKE", "DENY", "USER", "ROLE", "PERMISSION", "CAST", "CONVERT", "SUBSTRING", "LEN",
            "UPPER", "LOWER", "TRIM", "REPLACE", "DATEADD", "DATEDIFF", "GETDATE", "YEAR", "MONTH", "DAY",
            "ID", "CREATEDAT", "UPDATEDAT" // Sistem sütunları
        };

        public CreateColumnRequestValidator()
        {
            RuleFor(x => x.ColumnName)
                .NotEmpty().WithMessage("Sütun adı boş olamaz.")
                .MaximumLength(50).WithMessage("Sütun adı 50 karakterden fazla olamaz.")
                .Must(BeValidSqlIdentifier).WithMessage("Sütun adı geçerli bir SQL tanımlayıcısı olmalıdır. Sadece harf, rakam ve alt çizgi kullanabilirsiniz.")
                .Must(NotBeSqlReservedWord).WithMessage("Sütun adı SQL ayrılmış kelimesi olamaz.")
                .Must(StartWithLetter).WithMessage("Sütun adı harf ile başlamalıdır.");

            RuleFor(x => x.DataType)
                .IsInEnum().WithMessage("Geçerli bir veri tipi seçiniz.");

            RuleFor(x => x.DisplayOrder)
                .GreaterThan(0).WithMessage("Sıralama değeri 0'dan büyük olmalıdır.")
                .LessThan(1000).WithMessage("Sıralama değeri 1000'den küçük olmalıdır.");

            RuleFor(x => x.DefaultValue)
                .MaximumLength(255).WithMessage("Varsayılan değer 255 karakterden fazla olamaz.");

            // Veri tipine göre default value kontrolü
            RuleFor(x => x.DefaultValue)
                .Must((model, defaultValue) => ValidateDefaultValueByDataType(defaultValue, model.DataType))
                .WithMessage("Varsayılan değer, seçilen veri tipine uygun olmalıdır.")
                .When(x => !string.IsNullOrEmpty(x.DefaultValue));

            // SQL injection koruması
            RuleFor(x => x.DefaultValue)
                .Must(NotContainSqlInjection)
                .WithMessage("Varsayılan değer güvenlik açığı oluşturacak karakterler içeremez.")
                .When(x => !string.IsNullOrEmpty(x.DefaultValue));
        }

        private bool BeValidSqlIdentifier(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return _safeNamePattern.IsMatch(name);
        }

        private bool NotBeSqlReservedWord(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return !_sqlReservedWords.Contains(name);
        }

        private bool StartWithLetter(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return char.IsLetter(name[0]);
        }

        private bool ValidateDefaultValueByDataType(string? defaultValue, ColumnDataType dataType)
        {
            if (string.IsNullOrEmpty(defaultValue))
                return true; // Boş değer her zaman geçerli

            return dataType switch
            {
                ColumnDataType.Int => int.TryParse(defaultValue, out _),
                ColumnDataType.Decimal => decimal.TryParse(defaultValue, out _),
                ColumnDataType.DateTime => DateTime.TryParse(defaultValue, out _),
                ColumnDataType.Varchar => defaultValue.Length <= 255,
                _ => true
            };
        }

        private bool NotContainSqlInjection(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            // Tehlikeli karakterler ve kalıplar
            var dangerousPatterns = new[]
            {
                "'", "\"", ";", "--", "/*", "*/", "xp_", "sp_",
                "exec", "execute", "select", "insert", "update", "delete",
                "drop", "create", "alter", "union", "script", "<", ">"
            };

            var lowerValue = value.ToLowerInvariant();
            return !dangerousPatterns.Any(pattern => lowerValue.Contains(pattern));
        }
    }
}