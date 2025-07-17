using AutoMapper;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;
using TableManagement.Core.Entities;

namespace TableManagement.Application.Mappings
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // Table mappings
            CreateMap<CustomTable, TableResponse>()
                .ForMember(dest => dest.Columns, opt => opt.MapFrom(src => src.Columns));

            CreateMap<CreateTableRequest, CustomTable>()
                .ForMember(dest => dest.Columns, opt => opt.Ignore())
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore());

            // Column mappings
            CreateMap<CustomColumn, ColumnResponse>();

            CreateMap<CreateColumnRequest, CustomColumn>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CustomTableId, opt => opt.Ignore())
                .ForMember(dest => dest.CustomTable, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // User mappings
            CreateMap<User, UserResponse>();
        }
    }

    // User response DTO if needed
    public class UserResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsEmailConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}