using Mapster;

namespace SampleApp;

public class MapsterConfig
{
    public static void Configure()
    {
        // Custom mapping configuration for Person to PersonDto
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => src.Id.ToString())
            .Map(dest => dest.Name, src => src.Name);
        
        TypeAdapterConfig<TypeWithNoCommonProps, AnotherTypeWithNoCommonProps>
            .NewConfig()
            .Map(dest => dest.PropertyX, src => src.PropertyA)
            .Map(dest => dest.PropertyY, src => src.PropertyB);
    }
    
    
}