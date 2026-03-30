using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RankingCalculator.Models
{
    public class Competition
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public double Coef {  get; set; }
    }
}
