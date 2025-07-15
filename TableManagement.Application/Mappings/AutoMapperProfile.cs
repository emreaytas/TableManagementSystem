using AutoMapper;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.Entities;

namespace TableManagement.Application.Mappings
{
    
    
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<User, UserDto>();
            CreateMap<CustomTable, TableResponse>();
            CreateMap<CustomColumn, ColumnResponse>();
        }
    }


}
