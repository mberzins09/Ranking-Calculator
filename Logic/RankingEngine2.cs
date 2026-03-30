using RankingCalculator.Data;
using RankingCalculator.Models;

namespace RankingCalculator.Logic
{
    public class RankingEngine2(Database db)
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

        public void CalculateCompetition(Competition comp, Dictionary<int, PRating> rating)
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

                EnsurePlayer(rating, p1);
                EnsurePlayer(rating, p2);

                int r1 = rating[p1].Points;
                int r2 = rating[p2].Points;

                double e1 = elo.Expected(r1, r2);
                double e2 = elo.Expected(r2, r1);

                double score1 = s1 > s2 ? 1 : 0;
                double score2 = s2 > s1 ? 1 : 0;

                double coef1 = comp.Coef;
                double coef2 = comp.Coef;

                var g1 = rating[p1].Gender;
                var g2 = rating[p2].Gender;

                if (g1 == "female" && g2 == "male")
                {
                    coef1 *= 2.0;
                }

                if (g2 == "female" && g1 == "male")
                {
                    coef2 *= 2.0;
                }

                if (g1 == "female" && g2 == "female")
                {
                    double trust1 = rating[p1].GamesVsMale / (double)(rating[p1].GamesVsMale + rating[p1].GamesVsFemale + 1);

                    double trust2 = rating[p2].GamesVsMale / (double)(rating[p2].GamesVsMale + rating[p2].GamesVsFemale + 1);

                    coef1 *= 0.5 + trust1;
                    coef2 *= 0.5 + trust2;
                }

                int c1 = rating[p1].Competitions;
                int c2 = rating[p2].Competitions;

                if (c1 < 5) coef1 *= 2;
                if (c2 < 5) coef2 *= 2;

                if (c1 > 50) coef1 *= 0.7;
                if (c2 > 50) coef2 *= 0.7;

                rating[p1].Points = elo.NewRating(r1, score1, e1, coef1);
                rating[p2].Points = elo.NewRating(r2, score2, e2, coef2);

                rating[p1].CompetitionDates.Add(comp.StartDate);
                rating[p2].CompetitionDates.Add(comp.StartDate);
            }
        }

        public void ApplyYearPenalty(Dictionary<int, PRating> rating, DateTime currentDate)
        {
            if (currentDate.Month != 1)
                return;

            if (currentDate.Day != 1)
                return;

            int year = currentDate.Year;

            foreach (var p in rating.Values)
            {
                if (p.LastPenaltyYear == year)
                    continue;

                int last2y = p.CompetitionDates.Where(d => d >= currentDate.AddYears(-2)).Count();

                if (last2y < 2)
                {
                    p.Points -= 50;

                    if (p.Points < 500)
                        p.Points = 500;

                    p.LastPenaltyYear = year;
                }
            }
        }

        private void EnsurePlayer(Dictionary<int, PRating> rating, int id)
        {
            if (!rating.ContainsKey(id))
            {
                rating[id] = new PRating
                {
                    Points = 1000,
                    Competitions = 0,
                    GamesPlayed = 0,
                    Gender = null,
                    GamesVsMale = 0,
                    GamesVsFemale = 0
                };
            }
        }

        public void SaveMonth(string month, Dictionary<int, PRating> rating, string tableName)
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
                cmd.Parameters.AddWithValue("$pts", p.Value.Points);

                cmd.ExecuteNonQuery();
            }

            tr.Commit();
        }

        public Dictionary<int, PRating> LoadInitialRatings()
        {
            var rating = new Dictionary<int, PRating>();

            using var con = db_.GetSource();
            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText =
            """
    SELECT id, initial_points, Gender
    FROM players
    """;

            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                int id = r.GetInt32(0);

                int pts = 1000;

                if (!r.IsDBNull(1))
                    pts = r.GetInt32(1);

                string? gender = null;

                if (!r.IsDBNull(2))
                    gender = r.GetString(2);

                rating[id] = new PRating
                {
                    Points = pts - ((gender == "female") ? 300 : 0),
                    Competitions = 0,
                    GamesPlayed = 0,
                    Gender = gender,
                    GamesVsFemale = 0,
                    GamesVsMale = 0,
                };
            }

            return rating;
        }

        public Dictionary<int, PlayerResult> GetCompetitionResults(Competition comp, Dictionary<int, PRating> rating)
        {
            var result = new Dictionary<int, PlayerResult>();

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

                EnsurePlayer(rating, p1);
                EnsurePlayer(rating, p2);

                int s1 = r.GetInt32(2);
                int s2 = r.GetInt32(3);

                if (!result.ContainsKey(p1))
                    result[p1] = new PlayerResult();

                if (!result.ContainsKey(p2))
                    result[p2] = new PlayerResult();

                result[p1].PlayerId = p1;
                result[p2].PlayerId = p2;

                int r1 = rating.ContainsKey(p1) ? rating[p1].Points : 1000;
                int r2 = rating.ContainsKey(p2) ? rating[p2].Points : 1000;

                result[p1].StartRating = r1;
                result[p2].StartRating = r2;

                int c1 = rating.ContainsKey(p1) ? rating[p1].Competitions : 0;
                int c2 = rating.ContainsKey(p2) ? rating[p2].Competitions : 0;

                string? g1 = rating.ContainsKey(p1) ? rating[p1].Gender : null;
                string? g2 = rating.ContainsKey(p2) ? rating[p2].Gender : null;

                result[p1].Gender = g1;
                result[p2].Gender = g2;

                if (g2 == "male")
                    rating[p1].GamesVsMale++;
                else if (g2 == "female")
                    rating[p1].GamesVsFemale++;

                if (g1 == "male")
                    rating[p2].GamesVsMale++;
                else if (g1 == "female")
                    rating[p2].GamesVsFemale++;

                rating[p1].GamesPlayed++;
                rating[p2].GamesPlayed++;

                rating[p1].LastCompetitionDate = comp.StartDate;
                rating[p2].LastCompetitionDate = comp.StartDate;

                if (s1 > s2)
                {
                    result[p1].WinsVs.Add((r2, c2, g2));
                    result[p2].LossVs.Add((r1, c1, g1));
                }
                else
                {
                    result[p2].WinsVs.Add((r1, c1, g1));
                    result[p1].LossVs.Add((r2, c2, g2));
                }
            }

            return result;
        }

        public int CalculateInitialFromResults(PlayerResult p)
        {
            int w = p.WinsVs.Count;
            int l = p.LossVs.Count;

            if (w == 0 && l == 0)
                return p.StartRating;

            if (w == 0)
            {
                int minOpp = p.LossVs.Min(x => x.rating);

                if (minOpp > 1000)
                    return 1000;

                return minOpp - 1;
            }

            if (l == 0)
            {
                int maxOpp = p.WinsVs.Max(x => x.rating);

                if (maxOpp < 1000)
                    return 1000;

                return maxOpp + 100;
            }

            int n = Math.Min(w, l);

            var wins = p.WinsVs.OrderByDescending(x => x.rating).Take(n);
            var losses = p.LossVs.OrderBy(x => x.rating).Take(n);
            var all = wins.Concat(losses);

            double sum = 0;
            double weightSum = 0;

            foreach (var a in all)
            {
                double weight = a.comps + 1;

                sum += a.rating * weight;
                weightSum += weight;
            }

            return (int)(sum / weightSum);
        }

        public int? CheckCorrection(PlayerResult p)
        {
            int baseRating = p.StartRating;

            var wins = p.WinsVs.Where(x => x.comps >= 1).ToList();
            var losses = p.LossVs.Where(x => x.comps >= 1).ToList();

            if (wins.Count == 0 && losses.Count == 0)
                return null;

            var bigWins =p.WinsVs.Where(x => x.rating - baseRating > 200).ToList();

            //var bigLoss =p.LossVs.Where(x => baseRating - x.rating > 300).ToList();

            if (bigWins.Count >= 2)
            {
                var all = bigWins.Concat(losses).ToList();

                if (all.Count < 2)
                    return null;

                return (int)all.Average(x => x.rating);
            }

            //if (bigLoss.Count >= 2)
            //{
            //    var normalWins = wins.Where(x => Math.Abs(x.rating - baseRating) <= 50).ToList();
            //    var all = bigLoss.Concat(normalWins).ToList();

            //    if (all.Count < 2)
            //        return null;

            //    return (int)all.Average(x => x.rating);
            //}

            return null;
        }

        public int? CheckCorrectionDown(PlayerResult p)
        {
            int baseRating = p.StartRating;

            var losses = p.LossVs.Where(x => x.comps >= 1).ToList();

            if (losses.Count == 0)
                return null;

            var bigLoss = losses
                .Where(x => baseRating - x.rating > 200)
                .ToList();

            if (bigLoss.Count >= 2)
            {
                var normalWins = p.WinsVs
                    .Where(x => Math.Abs(x.rating - baseRating) <= 50)
                    .ToList();

                var all = bigLoss.Concat(normalWins).ToList();

                if (all.Count < 2)
                    return null;

                return (int)all.Average(x => x.rating);
            }

            return null;
        }

        public int? CheckCorrectionUp(PlayerResult p)
        {
            int baseRating = p.StartRating;

            var wins = p.WinsVs.Where(x => x.comps >= 1).ToList();

            if (wins.Count == 0)
                return null;

            var bigWins = wins
                .Where(x => x.rating - baseRating > 200)
                .ToList();

            if (bigWins.Count >= 2)
            {
                var all = bigWins.Concat(p.LossVs).ToList();

                if (all.Count < 2)
                    return null;

                return (int)all.Average(x => x.rating);
            }

            return null;
        }

        public void CalculateCompetitionAdvanced(Competition comp, Dictionary<int, PRating> rating)
        {
            for (int iter = 0; iter < 5; iter++)
            {
                var results = GetCompetitionResults(comp, rating);
                bool changed = false;

                foreach (var p in results.Values)
                {
                    if (!rating.ContainsKey(p.PlayerId) || rating[p.PlayerId].Competitions == 0)
                    {
                        int newRating = CalculateInitialFromResults(p);
                        int gamesCount = p.WinsVs.Count() + p.LossVs.Count();
                        if (!rating.ContainsKey(p.PlayerId))
                        {
                            rating[p.PlayerId] = new PRating
                            {
                                Points = newRating,
                                GamesPlayed = gamesCount,
                                Competitions = 0,
                                GamesVsFemale = p.WinsVs.Count(x => x.gender == "female") + p.LossVs.Count(x => x.gender == "female"),
                                GamesVsMale = p.WinsVs.Count(x => x.gender == "male") + p.LossVs.Count(x => x.gender == "male")
                            };
                        }
                        else
                        {
                            rating[p.PlayerId].Points = newRating;
                            rating[p.PlayerId].GamesPlayed = gamesCount;
                        }

                        changed = true;
                    }

                    var up = CheckCorrectionUp(p);
                    if (up.HasValue)
                    {
                        rating[p.PlayerId].Points = up.Value;
                        changed = true;
                    }

                    var down = CheckCorrectionDown(p);
                    if (down.HasValue)
                    {
                        rating[p.PlayerId].Points = down.Value;
                        changed = true;
                    }
                }

                CalculateCompetition(comp, rating);

                foreach (var p in results.Keys)
                {
                    rating[p].Competitions++;
                }

                if (!changed)
                    break;
            }
        }

        public void Run(string tableName)
        {
            var months = GetCompetitionsByMonth();

            var rating = LoadInitialRatings();

            foreach (var m in months)
            {
                DateTime monthDate = DateTime.Parse(m.Key + "-01");

                foreach (var comp in m.Value)
                {
                    CalculateCompetitionAdvanced(comp, rating);
                }

                Console.WriteLine($"Writing data for {m.Key}");

                SaveMonth(m.Key, rating, tableName);
            }

            Console.WriteLine($"Rating Saved sucessfully in table {tableName}");
        }
    }
}
