using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RankingCalculator.Models
{
    public class Game
    {
        public int Id { get; set; }

        public int CompetitionId { get; set; }

        public int Player1Id { get; set; }
        public int Player2Id { get; set; }

        public int Player1Sets { get; set; }
        public int Player2Sets { get; set; }
    }
}
