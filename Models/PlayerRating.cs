using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RankingCalculator.Models
{
    public class PlayerRating
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Key_name { get; set; }

        public int Points { get; set; }

        public DateTime Month { get; set; }
    }
}
