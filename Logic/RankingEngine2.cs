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

                if (g1 == "female" && g2 == "female")
                {
                    double trust1 = rating[p1].GamesVsMale / (double)(rating[p1].GamesVsMale + rating[p1].GamesVsFemale + 1);

                    double trust2 = rating[p2].GamesVsMale / (double)(rating[p2].GamesVsMale + rating[p2].GamesVsFemale + 1);

                    coef1 *= 0.3 + trust1;
                    coef2 *= 0.3 + trust2;
                }

                int c1 = rating[p1].Competitions;
                int c2 = rating[p2].Competitions;

                if (c1 <= 10) coef1 *= 1.5;
                if (c2 <= 10) coef2 *= 1.5;

                if (c1 > 10 && c1 < 30) coef1 *= 1.2;
                if (c2 > 10 && c2 < 30) coef2 *= 1.2;

                coef1 = Math.Min(coef1, 10);
                coef2 = Math.Min(coef2, 10);

                rating[p1].Points = Math.Max(200, elo.NewRating(r1, score1, e1, coef1));
                rating[p2].Points = Math.Max(200, elo.NewRating(r2, score2, e2, coef2));

                rating[p1].CompetitionDates.Add(comp.StartDate);
                rating[p2].CompetitionDates.Add(comp.StartDate);
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
        points INTEGER,
        is_active INTEGER
    )
    """;

            create.ExecuteNonQuery();

            using var tr = con.BeginTransaction();

            DateTime currentMonth = DateTime.Parse(month + "-01");
            DateTime threshold = currentMonth.AddYears(-2);

            foreach (var p in rating)
            {
                var cmd = con.CreateCommand();

                bool isActive = p.Value.LastCompetitionDate != null && p.Value.LastCompetitionDate >= threshold;

                cmd.CommandText =
                $"""
        INSERT INTO {tableName}
        (player_id, month, points, is_active)
        VALUES ($p, $m, $pts, $active)
        """;

                cmd.Parameters.AddWithValue("$p", p.Key);
                cmd.Parameters.AddWithValue("$m", month);
                cmd.Parameters.AddWithValue("$pts", p.Value.Points);
                cmd.Parameters.AddWithValue("$active", isActive ? 1 : 0);

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

                int femalePoints = 0;

                if (gender == "female")
                {
                    if (pts > 2000)
                    {
                        femalePoints = pts - 400;
                    }
                    else if (pts > 1500)
                    {
                        femalePoints = pts - 200;
                    }
                    else
                    {
                        femalePoints = pts;
                    }
                }
                
                rating[id] = new PRating
                {
                    Points = (gender == "female") ? femalePoints : pts,
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

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int player1 = reader.GetInt32(0);
                int player2 = reader.GetInt32(1);

                EnsurePlayer(rating, player1);
                EnsurePlayer(rating, player2);

                int s1 = reader.GetInt32(2);
                int s2 = reader.GetInt32(3);

                if (!result.ContainsKey(player1))
                {
                    result[player1] = new PlayerResult();
                    result[player1].Coef = comp.Coef;
                }

                if (!result.ContainsKey(player2))
                {
                    result[player2] = new PlayerResult();
                    result[player2].Coef = comp.Coef;
                }

                result[player1].PlayerId = player1;
                result[player2].PlayerId = player2;

                int r1 = rating.ContainsKey(player1) ? rating[player1].Points : 1000;
                int r2 = rating.ContainsKey(player2) ? rating[player2].Points : 1000;

                result[player1].StartRating = r1;
                result[player2].StartRating = r2;

                int c1 = rating.ContainsKey(player1) ? rating[player1].Competitions : 0;
                int c2 = rating.ContainsKey(player2) ? rating[player2].Competitions : 0;

                string? g1 = rating.ContainsKey(player1) ? rating[player1].Gender : null;
                string? g2 = rating.ContainsKey(player2) ? rating[player2].Gender : null;

                result[player1].Gender = g1;
                result[player2].Gender = g2;

                if (g2 == "male")
                    rating[player1].GamesVsMale++;
                else if (g2 == "female")
                    rating[player1].GamesVsFemale++;

                if (g1 == "male")
                    rating[player2].GamesVsMale++;
                else if (g1 == "female")
                    rating[player2].GamesVsFemale++;

                rating[player1].GamesPlayed++;
                rating[player2].GamesPlayed++;

                rating[player1].LastCompetitionDate = comp.StartDate;
                rating[player2].LastCompetitionDate = comp.StartDate;

                if (s1 > s2)
                {
                    result[player1].WinsVs.Add((player2, c2, g2));
                    result[player2].LossVs.Add((player1, c1, g1));
                }
                else if (s2 > s1)
                {
                    result[player2].WinsVs.Add((player1, c1, g1));
                    result[player1].LossVs.Add((player2, c2, g2));
                }
            }

            return result;
        }

        public int CalculateInitialFromResults(PlayerResult p, Dictionary<int, PRating> rating)
        {
            int w = p.WinsVs.Count;
            int l = p.LossVs.Count;
            bool bigComp;

            if (w == 0 && l == 0)
            {
                p.AfterInitial = p.StartRating;
                return p.AfterInitial;
            }

            if (w == 0)
            {
                int minOpp = p.LossVs.Min(x => rating[x.opponentId].Points);

                if (minOpp > 1000)
                {
                    p.AfterInitial = 1000;
                    return p.AfterInitial = 1000;
                    ;
                }

                p.AfterInitial = minOpp - 1;
                return p.AfterInitial;
            }

            if (l == 0)
            {
                int maxOpp = p.WinsVs.Max(x => rating[x.opponentId].Points);

                if (maxOpp < p.StartRating)
                {
                    p.AfterInitial = p.StartRating;
                    return p.AfterInitial;
                }

                if (maxOpp < 1000)
                {
                    p.AfterInitial = 1000;
                    return p.AfterInitial;
                }

                bigComp = p.Coef >= 2;

                if (bigComp)
                {
                    if (p.Gender == null)
                    {
                        p.AfterInitial = maxOpp + 400;
                        return p.AfterInitial;
                    }

                    p.AfterInitial = maxOpp + 200;
                    return p.AfterInitial;
                }

                if (p.Gender == null)
                {
                    p.AfterInitial = maxOpp + 300;
                    return p.AfterInitial;
                }

                p.AfterInitial = maxOpp + 100;
                return p.AfterInitial;
            }

            int n = Math.Min(w, l);

            var wins = p.WinsVs.OrderByDescending(x => rating[x.opponentId].Points).Take(n);
            var losses = p.LossVs.OrderBy(x => rating[x.opponentId].Points).Take(n);
            var all = wins.Concat(losses);

            double sum = 0;
            double weightSum = 0;

            foreach (var a in all)
            {
                int oppRating = rating[a.opponentId].Points;
                int comps = a.comps;

                double weight = comps + 1;

                sum += oppRating * weight;
                weightSum += weight;
            }

            bigComp = p.Coef >= 2;
            var weightSumReturn = (int)(sum / weightSum);

            if (bigComp)
            {
                if (p.Gender == null)
                {
                    p.AfterInitial = 400 + weightSumReturn;
                    return p.AfterInitial;
                }

                p.AfterInitial = 50 + weightSumReturn;
                return p.AfterInitial;
            }

            if (p.Gender == null)
            {
                p.AfterInitial = 300 + weightSumReturn;
                return p.AfterInitial;
            }

            p.AfterInitial = 20 + weightSumReturn;
            return p.AfterInitial;
        }

        public int? CheckCorrectionUp(PlayerResult p, Dictionary<int, PRating> rating)
        {
            int baseRating = p.StartRating;

            if (p.AfterInitial > p.StartRating)
            {
                baseRating = p.AfterInitial;
            }

            var wins = p.WinsVs.Where(x => x.comps >= 1).ToList();
            var losses = p.LossVs.ToList();

            if (wins.Count == 0)
                return null;

            var bigWins = wins.Where(x => rating[x.opponentId].Points - baseRating >= 400).ToList();
            var superWins = wins.Where(x => rating[x.opponentId].Points - baseRating >= 600).ToList();
            bool isSuperWin = superWins.Count > 1;
            var countedLosses = losses.Where(x => Math.Abs(rating[x.opponentId].Points - baseRating) <= 200);

            if (bigWins.Count >= 2 || isSuperWin)
            {
                var all = isSuperWin ? wins.Concat(p.LossVs).ToList() : bigWins.Concat(countedLosses).ToList();

                if (all.Count < 2)
                    return null;

                var avarageRating = (int)all.Average(x => rating[x.opponentId].Points);

                if (avarageRating < baseRating)
                    return null;

                if (avarageRating - baseRating > 400)
                    return baseRating + 400;

                return avarageRating;
            }

            return null;
        }

        public int? CheckCorrectionDown(PlayerResult p, Dictionary<int, PRating> rating)
        {
            int baseRating = p.StartRating;

            if (p.AfterInitial > p.StartRating)
            {
                baseRating = p.AfterInitial;
            }

            var losses = p.LossVs.Where(x => x.comps >= 1).ToList();
            var wins = p.WinsVs.ToList();

            if (losses.Count == 0)
                return null;

            var bigLosses = losses.Where(x => baseRating - rating[x.opponentId].Points >= 500).ToList();
            var countedWins = wins.Where(x => Math.Abs(rating[x.opponentId].Points - baseRating) <= 200);

            if (bigLosses.Count >= 4)
            {
                var all = bigLosses.Concat(countedWins).ToList();

                if (all.Count < 4)
                    return null;

                return (int)all.Average(x => rating[x.opponentId].Points);
            }

            return null;
        }

        public void CalculateCompetitionAdvanced(Competition comp, Dictionary<int, PRating> rating)
        {
            //var results = GetCompetitionResults(comp, rating);

            //for (int iter = 0; iter < 2; iter++)
            //{
            //    bool changed = false;

            //    foreach (var p in results.Values)
            //    {
            //        if (!rating.ContainsKey(p.PlayerId) || rating[p.PlayerId].Competitions == 0)
            //        {
            //            int newRating = CalculateInitialFromResults(p, rating);
            //            int gamesCount = p.WinsVs.Count() + p.LossVs.Count();
            //            if (!rating.ContainsKey(p.PlayerId))
            //            {
            //                rating[p.PlayerId] = new PRating
            //                {
            //                    Points = newRating,
            //                    GamesPlayed = gamesCount,
            //                    Competitions = 0,
            //                    GamesVsFemale = p.WinsVs.Count(x => x.gender == "female") + p.LossVs.Count(x => x.gender == "female"),
            //                    GamesVsMale = p.WinsVs.Count(x => x.gender == "male") + p.LossVs.Count(x => x.gender == "male")
            //                };
            //            }
            //            else
            //            {
            //                rating[p.PlayerId].Points = newRating;
            //                rating[p.PlayerId].GamesPlayed = gamesCount;
            //            }

            //            changed = true;
            //        }

            //        var up = CheckCorrectionUp(p, rating);
            //        if (up.HasValue)
            //        {
            //            rating[p.PlayerId].Points = up.Value;
            //            changed = true;
            //        }

            //        var down = CheckCorrectionDown(p, rating);
            //        if (down.HasValue)
            //        {
            //            rating[p.PlayerId].Points = down.Value;
            //            changed = true;
            //        }
            //    }

            //    CalculateCompetition(comp, rating);

            //    if (!changed)
            //        break;
            //}

            //foreach (var p in results.Keys)
            //{
            //    rating[p].Competitions++;
            //}

            var results = GetCompetitionResults(comp, rating);

            var snapshot = rating.ToDictionary(x => x.Key, x => x.Value.Points);
            var corrected = new Dictionary<int, int>();

            foreach (var p in results.Values)
            {
                int newRating = snapshot[p.PlayerId];

                if (!rating.ContainsKey(p.PlayerId) || rating[p.PlayerId].Competitions == 0)
                {
                    newRating = CalculateInitialFromResults(p, rating);
                }

                var up = CheckCorrectionUp(p, rating);
                if (up.HasValue)
                    newRating = up.Value;

                var down = CheckCorrectionDown(p, rating);
                if (down.HasValue)
                    newRating = down.Value;

                corrected[p.PlayerId] = newRating;
            }

            foreach (var kv in corrected)
            {
                rating[kv.Key].Points = kv.Value;
            }

            CalculateCompetition(comp, rating);

            foreach (var p in results.Keys)
            {
                rating[p].Competitions++;
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
