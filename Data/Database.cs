using Microsoft.Data.Sqlite;
using RankingCalculator.Models;

namespace RankingCalculator.Data
{
    public class Database
    {
        private string source;
        private string target;

        public Database()
        {
            source = $"Data Source={Path.Combine(AppContext.BaseDirectory,"lgtf.sqlite")}";

            target = $"Data Source={Path.Combine(AppContext.BaseDirectory,"output.sqlite")}";
        }

        public SqliteConnection GetSource() => new(source);

        public SqliteConnection GetTarget() => new(target);

        public async Task AddPoints(List<PlayerApi> playersApi)
        {
            using var con = GetSource();
            con.Open();

            var alter = con.CreateCommand();

            alter.CommandText =
            """
        ALTER TABLE players
        ADD COLUMN initial_points INTEGER
        """;

            alter.ExecuteNonQuery();

            // ---------- 2. update players ----------

            using var tr = con.BeginTransaction();

            foreach (var p in playersApi)
            {
                var cmd = con.CreateCommand();

                cmd.CommandText =
                """
        UPDATE players
        SET initial_points = $pts
        WHERE id = $id
        """;

                cmd.Parameters.AddWithValue(
                    "$pts",
                    p.Points + 1000);

                cmd.Parameters.AddWithValue(
                    "$id",
                    p.PlayerId);

                cmd.ExecuteNonQuery();
            }

            tr.Commit();
        }

        public int GetOrCreatePlayer(string name, string surname)
        {
            string key = NameNormalizer.Normalize($"{name}{surname}");

            using var con = GetSource();
            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText =
            """
    SELECT id
    FROM players
    WHERE key_name = $k
    """;

            cmd.Parameters.AddWithValue("$k", key);

            var id = cmd.ExecuteScalar();

            if (id != null)
                return Convert.ToInt32(id);

            // insert new player

            var ins = con.CreateCommand();

            ins.CommandText =
            """
    INSERT INTO players(name, surname, key_name)
    VALUES($n,$s,$k);
    SELECT last_insert_rowid();
    """;

            ins.Parameters.AddWithValue("$n", name);
            ins.Parameters.AddWithValue("$s", surname);
            ins.Parameters.AddWithValue("$k", key);

            return Convert.ToInt32(
                ins.ExecuteScalar());
        }

        public int InsertCompetition(string name, string date, double coef)
        {
            using var con = GetSource();
            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText =
            """
    INSERT INTO competitions
    (name,start_date,coef)
    VALUES($n,$d,$c);

    SELECT last_insert_rowid();
    """;

            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$d", date);
            cmd.Parameters.AddWithValue("$c", coef);

            return Convert.ToInt32(
                cmd.ExecuteScalar());
        }

        public void InsertGame(int compId, int p1, int p2, int s1, int s2)
        {
            using var con = GetSource();
            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText =
            """
    INSERT INTO games
    (competition_id,
     player1_id,
     player2_id,
     player1_sets,
     player2_sets)
    VALUES($c,$p1,$p2,$s1,$s2)
    """;

            cmd.Parameters.AddWithValue("$c", compId);
            cmd.Parameters.AddWithValue("$p1", p1);
            cmd.Parameters.AddWithValue("$p2", p2);
            cmd.Parameters.AddWithValue("$s1", s1);
            cmd.Parameters.AddWithValue("$s2", s2);

            cmd.ExecuteNonQuery();
        }

        public async Task ImportAll()
        {
            Console.WriteLine("Start import");

            var api = new TournamentApiService();
            var db = new Database();

            Console.WriteLine("Getting event ids");
            var events = await api.GetAllEventIds();

            Console.WriteLine($"Got {events.Count} events");

            foreach (var id in events)
            {
                Console.WriteLine($"Loading event {id}");
                var ev = await api.GetEvent(id);
                Console.WriteLine("Loaded JSON");

                if (ev == null)
                {
                    Console.WriteLine($"event null continue");
                    continue;
                }

                if (ev.nets == null)
                {
                    Console.WriteLine($"event nets null continue");
                    continue;
                }

                Console.WriteLine($"Inserting event {ev.competition_event.name}");

                int compId = db.InsertCompetition(ev.competition_event.name, ev.competition_event.start_date, double.Parse(ev.competition_event.ranking_coef));

                Console.WriteLine($"Importing event {ev.competition_event.name}");

                foreach (var net in ev.nets)
                {
                    if (net.groups != null)
                    {
                        foreach (var g in net.groups)
                        {
                            foreach (var game in g.games ?? [])
                            {
                                ImportGame(game, compId, db);
                            }
                        }
                    }

                    if (net.elimination_trees != null)
                    {
                        foreach (var tree in net.elimination_trees)
                        {
                            foreach (var round in tree.rounds ?? [])
                            {
                                foreach (var game in round.games ?? [])
                                {
                                    ImportGame(game, compId, db);
                                }
                            }
                        }
                    }

                    continue;
                }

                Console.WriteLine("Import finished");
            }
        }

        void ImportGame(GroupGame game, int compId, Database db)
        {
            if (game == null)
                return;

            if (game.game_type != "singles")
                return;

            if (game.player1 == null || game.player2 == null)
                return;

            if (!int.TryParse(game.player1_score, out int s1))
                return;

            if (!int.TryParse(game.player2_score, out int s2))
                return;

            int p1 = db.GetOrCreatePlayer(game.player1.name, game.player1.surname);

            int p2 = db.GetOrCreatePlayer(game.player2.name, game.player2.surname);

            db.InsertGame(compId, p1, p2, s1, s2);
        }
    }
}
