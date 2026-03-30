using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RankingCalculator.Data
{
    public class AddInitialPoints
    {

        public async Task AddInitialPointsToSource()
        {
            var api = new PlayerApiService();
            var men = await api.GetPlayersAsync("virietis", "2014-01");
            var women = await api.GetPlayersAsync("sieviete", "2014-01");

            var all = men.Concat(women).ToList();

            var db = new Database();

            await db.AddPoints(all);
        }
    }
}
