using Mapster;

namespace SampleApp;

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
    }
}