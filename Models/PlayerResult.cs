namespace RankingCalculator.Models
{
    public class PlayerResult
    {
        public int PlayerId { get; set; }

        public List<(int opponentId, int comps, string? gender)> WinsVs = [];

        public List<(int opponentId, int comps, string? gender)> LossVs = [];

        public int StartRating { get; set; }

        public string? Gender { get; set; }
    }
}
