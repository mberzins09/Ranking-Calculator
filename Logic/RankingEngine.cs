using RankingCalculator.Data;
using RankingCalculator.Models;

namespace RankingCalculator.Logic
{
    public class RankingEngine(Database db)
    {
        private readonly Database db_ = db;

        public Dictionary<string, List<Competition>> GetCompetitionsByMonth()
        {
            var result = new Dictionary<string, List<Competition>>();

            using var con = db_.GetSource();
            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText =
            """
        SELECT id, start_date, coef
        FROM competitions
        ORDER BY start_date
        """;

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var comp = new Competition
                {
                    Id = reader.GetInt32(0),
                    StartDate = DateTime.Parse(reader.GetString(1)),
                    Coef = reader.GetDouble(2)
                };

                string key =
                    comp.StartDate.ToString("yyyy-MM");

                if (!result.ContainsKey(key))
                    result[key] = new List<Competition>();

                result[key].Add(comp);
            }

            return result;
        }

        public void CalculateCompetition(Competition comp, Dictionary<int, int> rating)
        {
            var elo = new EloCalculator();

            using var con = db_.GetSource();
            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText =
            """
    SELECT player1_id,
           player2_id,
           player1_sets,
           player2_sets
    FROM games
    WHERE competition_id = $id
    """;

            cmd.Parameters.AddWithValue("$id", comp.Id);

            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                int p1 = r.GetInt32(0);
                int p2 = r.GetInt32(1);

                int s1 = r.GetInt32(2);
                int s2 = r.GetInt32(3);

                if (!rating.ContainsKey(p1))
                    rating[p1] = 1000;

                if (!rating.ContainsKey(p2))
                    rating[p2] = 1000;

                int r1 = rating[p1];
                int r2 = rating[p2];

                double e1 = elo.Expected(r1, r2);
                double e2 = elo.Expected(r2, r1);

                double score1 = s1 > s2 ? 1 : 0;
                double score2 = s2 > s1 ? 1 : 0;

                rating[p1] = elo.NewRating(r1, score1, e1, comp.Coef);
                rating[p2] = elo.NewRating(r2, score2, e2, comp.Coef);
            }
        }

        public void SaveMonth(string month, Dictionary<int, int> rating, string tableName)
        {
            using var con = db_.GetTarget();
            con.Open();

            var create = con.CreateCommand();

            create.CommandText =
            $"""
    CREATE TABLE IF NOT EXISTS {tableName}
    (
        player_id INTEGER,
        month TEXT,
        points INTEGER
    )
    """;

            create.ExecuteNonQuery();

            using var tr = con.BeginTransaction();

            foreach (var p in rating)
            {
                var cmd = con.CreateCommand();

                cmd.CommandText =
                $"""
        INSERT INTO {tableName}
        (player_id, month, points)
        VALUES ($p, $m, $pts)
        """;

                cmd.Parameters.AddWithValue("$p", p.Key);
                cmd.Parameters.AddWithValue("$m", month);
                cmd.Parameters.AddWithValue("$pts", p.Value);

                cmd.ExecuteNonQuery();
            }

            tr.Commit();
        }

        public Dictionary<int, int> LoadInitialRatings()
{
            var rating = new Dictionary<int, int>();

            using var con = db_.GetSource();
            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText =
            """
    SELECT id, initial_points
    FROM players
    """;

            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                int id = r.GetInt32(0);

                int pts = 1000;

                if (!r.IsDBNull(1))
                    pts = r.GetInt32(1);

                rating[id] = pts;
            }

            return rating;
        }

        public void Run()
        {
            var months = GetCompetitionsByMonth();

            //var rating = new Dictionary<int, int>();
            var rating = LoadInitialRatings();

            foreach (var m in months)
            {
                foreach (var comp in m.Value)
                {
                    CalculateCompetition(comp, rating);
                }

                Console.WriteLine($"Writing data for {m.Key}");

                SaveMonth(m.Key, rating, "ratings_with_initial");
            }
        }
    }
}
