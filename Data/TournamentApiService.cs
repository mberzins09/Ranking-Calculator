using RankingCalculator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace RankingCalculator.Data
{
    public class TournamentApiService
    {
        private readonly HttpClient _http = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private int _requestCount = 0;

        private DateTime _windowStart = DateTime.Now;

        private const int LIMIT = 90;

        private const string BaseUrl = "https://turniri.lgtf.lv/api/v1";

        private const string ApiKey =
            "org_trJaxebjAq9bQjdkPb1PJONCO1Im8befEFv7w8Jr";

        public TournamentApiService()
        {
            _http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<List<int>> GetAllEventIds()
        {
            await WaitIfNeeded();

            var res = await _http.GetFromJsonAsync<CompetitionListResponse>($"{BaseUrl}/competitions");

            var ids = new List<int>();

            foreach (var c in res.competitions)
                foreach (var e in c.events)
                    ids.Add(e.id);

            return ids;
        }

        public async Task<EventResultResponse?>GetEvent(int eventId)
        {
            await WaitIfNeeded();

            var url = $"{BaseUrl}/competition-event-results?competition_event_id={eventId}";

            var resp = await _http.GetAsync(url);

            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine("429 received, waiting 60s");

                await Task.Delay(60000);

                return await GetEvent(eventId);
            }

            resp.EnsureSuccessStatusCode();

            return await resp.Content.ReadFromJsonAsync<EventResultResponse>();
        }

        private async Task WaitIfNeeded()
        {
            _requestCount++;

            var elapsed = DateTime.Now - _windowStart;

            if (_requestCount >= LIMIT)
            {
                if (elapsed.TotalSeconds < 60)
                {
                    int wait = 60 - (int)elapsed.TotalSeconds;

                    Console.WriteLine($"Rate limit reached. Waiting {wait}s");

                    await Task.Delay(wait * 1000);
                }

                _requestCount = 0;
                _windowStart = DateTime.Now;
            }
        }
    }
}
