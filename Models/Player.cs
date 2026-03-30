using System.Text.Json.Serialization;

namespace RankingCalculator.Models
{
    public class Player
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Key_name { get; set; }
    }
}
