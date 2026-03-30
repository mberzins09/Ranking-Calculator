
namespace RankingCalculator.Models
{
    public class PRating
    {
        public int Points { get; set; }

        public int GamesPlayed { get; set; }

        public int Competitions { get; set; }

        public string? Gender { get; set; }

        public int GamesVsMale { get; set; }

        public int GamesVsFemale { get; set; }

        public DateTime? LastCompetitionDate { get; set; }

        public HashSet<DateTime> CompetitionDates = [];

        public int LastPenaltyYear { get; set; } = 0;
    }
}
