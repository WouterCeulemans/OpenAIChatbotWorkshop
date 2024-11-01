using System.Text.Json.Serialization;

namespace OpenAIChatbot.Web.Objects
{
    public class WeatherForecastInputData
    {
        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;
    }
}