using AutoMapper;
using CatalogMicroservice.applicationCore.Entities;
using CatalogMicroservice.CatalogMicroservice.CatalogTypeEndpoints;
using CatalogMicroservice.publicAPI.CatalogBrandEndpoints;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;

namespace Microsoft.eShopWeb.PublicApi;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CatalogItem, CatalogItemDto>();
        CreateMap<CatalogType, CatalogTypeDto>()
            .ForMember(dto => dto.Name, options => options.MapFrom(src => src.Type));
        CreateMap<CatalogBrand, CatalogBrandDto>()
            .ForMember(dto => dto.Name, options => options.MapFrom(src => src.Brand));
    }
}
