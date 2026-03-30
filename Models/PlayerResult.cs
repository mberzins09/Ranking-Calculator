namespace RankingCalculator.Models
{
    public class PlayerResult
    {
        public int PlayerId { get; set; }

        public List<(int rating, int comps, string? gender)> WinsVs = [];

        public List<(int rating, int comps, string? gender)> LossVs = [];

        public int StartRating { get; set; }

        public int CurrentRating { get; set; }

        public string? Gender { get; set; }
    }
}
