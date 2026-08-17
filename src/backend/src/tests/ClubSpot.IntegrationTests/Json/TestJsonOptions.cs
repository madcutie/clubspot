using System.Text.Json;
using System.Text.Json.Serialization;
using ClubSpot.Domain.Bookings;
using ClubSpot.Domain.Core;
using ClubSpot.Domain.Core.People;

namespace ClubSpot.IntegrationTests.Json;

// Mirrors the API's converters: one per enum, so enum dictionary keys keep their original casing.
public static class TestJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter<PersonOrigin>(JsonNamingPolicy.CamelCase),
            new JsonStringEnumConverter<Sport>(JsonNamingPolicy.CamelCase),
            new JsonStringEnumConverter<Role>(JsonNamingPolicy.CamelCase)
        }
    };
}
