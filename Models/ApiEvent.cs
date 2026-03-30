using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RankingCalculator.Models
{
    public class ApiEvent
    {
        public int id { get; set; }
    }

    public class ApiCompetition
    {
        public int id { get; set; }
        public string name { get; set; }
        public string start_date { get; set; }
        public string ranking_coef { get; set; }

        public List<ApiEvent> events { get; set; }
    }

    public class CompetitionListResponse
    {
        public List<ApiCompetition> competitions { get; set; }
    }

    public class EventResultResponse
    {
        public CompetitionEvent competition_event { get; set; }
        public List<Net> nets { get; set; }
    }

    public class CompetitionEvent
    {
        public int id { get; set; }

        public string name { get; set; }

        public string start_date { get; set; }

        public string ranking_coef { get; set; }

        public CompetitionInfo competition { get; set; }
    }

    public class CompetitionInfo
    {
        public int id { get; set; }

        public string name { get; set; }
    }

    public class Participant
    {
    }

    public class Net
    {
        public int id { get; set; }

        public string type { get; set; }

        public List<Group>? groups { get; set; }

        public List<EliminationTree>? elimination_trees { get; set; }
    }

    public class Group
    {
        public int id { get; set; }

        public List<GroupGame>? games { get; set; }
    }

    public class GroupGame
    {
        public int id { get; set; }

        public string game_type { get; set; }

        public PlayerShort? player1 { get; set; }

        public PlayerShort? player2 { get; set; }

        public string player1_score { get; set; }

        public string player2_score { get; set; }
    }

    public class GameSingle
    {
        public Player player1 { get; set; }

        public Player player2 { get; set; }

        public string player1_score { get; set; }

        public string player2_score { get; set; }
    }

    public class EliminationTree
    {
        public int id { get; set; }

        public List<Round>? rounds { get; set; }
    }

    public class Round
    {
        public int round { get; set; }

        public List<GroupGame>? games { get; set; }
    }

    public class PlayerShort
    {
        public int id { get; set; }

        public string name { get; set; }

        public string surname { get; set; }
    }
}
