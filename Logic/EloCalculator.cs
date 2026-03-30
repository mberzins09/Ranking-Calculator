
using System.Buffers.Text;

namespace RankingCalculator.Logic
{
    public class EloCalculator
    {
        public int BaseK = 40;

        public double Expected(int ra, int rb)
        {
            return 1.0 /
                (1.0 + Math.Pow(10, (rb - ra) / 400.0));
        }

        public int NewRating(int rating, double score, double expected, double coef)
        {
            double k = BaseK * coef;

            return (int)Math.Round(
                rating + k * (score - expected)
            );
        }
    }
}
