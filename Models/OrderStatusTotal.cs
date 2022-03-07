using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class OrderStatusTotal
    {
        public int TotRP { get; set; }
        public int TotIP { get; set; }
        public int TotPS { get; set; }
        public int TotOH { get; set; }
        public int TotCN { get; set; }
        public int TotSH { get; set; }
        public int TotBO { get; set; }
        public int TotPB { get; set; }

        public int Tot30 { get; set; }
        public DateTime DaysAgo30 { get; set; }
        public int Tot60 { get; set; }
        public DateTime DaysAgo60 { get; set; }
        public int TotDay { get; set; }
        public DateTime Today { get; set; }
        public int TotWeek { get; set; }
        public DateTime WeekFirstDate { get; set; }
        public int TotMonth { get; set; }
        public DateTime MonthFirstDate { get; set; }
        public int TotQtr { get; set; }
        public DateTime QuarterFirstDate { get; set; }
        public int TotYtd { get; set; }
        public DateTime YearFirstDate { get; set; }
    }
}
