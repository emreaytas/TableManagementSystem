using AutoMapper;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.Entities;

namespace TableManagement.Application.Mappings
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            ConfigureMappings();
        }

        private void ConfigureMappings()
        {
            // User mappings
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName ?? string.Empty))
                .ForMember(dest => dest.IsEmailConfirmed, opt => opt.MapFrom(src => src.IsEmailConfirmed));

            // Table mappings
            CreateMap<CustomTable, TableResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.TableName, opt => opt.MapFrom(src => src.TableName))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description ?? string.Empty))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.Columns, opt => opt.MapFrom(src =>
                    src.Columns != null ? src.Columns.OrderBy(c => c.DisplayOrder).ToList() : new List<CustomColumn>()));

            // Column mappings
            CreateMap<CustomColumn, ColumnResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ColumnName, opt => opt.MapFrom(src => src.ColumnName))
                .ForMember(dest => dest.DataType, opt => opt.MapFrom(src => src.DataType))
                .ForMember(dest => dest.IsRequired, opt => opt.MapFrom(src => src.IsRequired))
                .ForMember(dest => dest.DisplayOrder, opt => opt.MapFrom(src => src.DisplayOrder))
                .ForMember(dest => dest.DefaultValue, opt => opt.MapFrom(src => src.DefaultValue ?? string.Empty));
        }
    }
}