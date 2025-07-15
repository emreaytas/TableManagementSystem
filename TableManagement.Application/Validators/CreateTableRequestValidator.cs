using FluentValidation;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Core.Enums;

namespace TableManagement.Application.Validators
{
    public class CreateTableRequestValidator : AbstractValidator<CreateTableRequest>
    {
        public CreateTableRequestValidator()
        {
            RuleFor(x => x.TableName)
                .NotEmpty().WithMessage("Tablo adı boş olamaz.")
                .MaximumLength(100).WithMessage("Tablo adı 100 karakterden fazla olamaz.")
                .Matches(@"^[a-zA-ZığüşöçİĞÜŞÖÇ0-9_\s]+$")
                .WithMessage("Tablo adı sadece harf, rakam, alt çizgi ve boşluk içerebilir.");

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
        }
    }

    public class CreateColumnRequestValidator : AbstractValidator<CreateColumnRequest>
    {
        public CreateColumnRequestValidator()
        {
            RuleFor(x => x.ColumnName)
                .NotEmpty().WithMessage("Sütun adı boş olamaz.")
                .MaximumLength(100).WithMessage("Sütun adı 100 karakterden fazla olamaz.")
                .Matches(@"^[a-zA-ZığüşöçİĞÜŞÖÇ0-9_]+$")
                .WithMessage("Sütun adı sadece harf, rakam ve alt çizgi içerebilir.");

            RuleFor(x => x.DataType)
                .IsInEnum().WithMessage("Geçerli bir veri tipi seçiniz.");

            RuleFor(x => x.DisplayOrder)
                .GreaterThan(0).WithMessage("Sıralama değeri 0'dan büyük olmalıdır.");

            RuleFor(x => x.DefaultValue)
                .MaximumLength(255).WithMessage("Varsayılan değer 255 karakterden fazla olamaz.");

            // Veri tipine göre default value kontrolü
            RuleFor(x => x.DefaultValue)
                .Must((model, defaultValue) => ValidateDefaultValueByDataType(defaultValue, model.DataType))
                .WithMessage("Varsayılan değer, seçilen veri tipine uygun olmalıdır.")
                .When(x => !string.IsNullOrEmpty(x.DefaultValue));
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
    }
}