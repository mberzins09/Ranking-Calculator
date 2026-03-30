using RankingCalculator.Models;
using System.Net.Http.Json;

namespace RankingCalculator.Data
{
    public class PlayerApiService
    {
        private static readonly HttpClient _httpClient = new();

        public async Task<List<PlayerApi>?> GetPlayersAsync(string gender, string date)
        {
            try
            {
                //if (DateTime.TryParseExact(date, "yyyy-MM", null,
                //    System.Globalization.DateTimeStyles.None, out var parsed))
                //{
                //    date = parsed.ToString("yyyy-MM");
                //}

                //string year = date.Split('-')[0];

                object requestBody = new { date, gender };

                var response = await _httpClient.PostAsJsonAsync("https://www.lgtf.lv/api/getRanking", requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API failed: {date} {gender} {response.StatusCode}");
                    return [];
                }

                var result = await response.Content.ReadFromJsonAsync<PlayersResponseDates>();

                return result?.Players ?? [];
            }
            catch
            {
                return [];
            }
        }
        public class PlayersResponseDates
        {
            public List<PlayerApi>? Players { get; set; }
        }
    }
}
