using Mapster;

namespace SampleApp;

public class MapsterConfig
{
    public static void Configure()
    {
        // Custom mapping configuration for Person to PersonDto
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
    }
}