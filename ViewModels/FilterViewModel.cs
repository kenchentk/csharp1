using StoreFront2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class FilterViewModel
    {
        public string OrderNumber { get; set; }
        public List<OrderStatus> SelectedStatuses;
        public string Status { get; set; }
        public Nullable<DateTime> OrderDateStart { get; set; }
        public Nullable<DateTime> OrderDateEnd { get; set; }
        public Nullable<DateTime> ShipDateStart { get; set; }
        public Nullable<DateTime> ShipDateEnd { get; set; }
        public string CreatedBy { get; set; }
        public bool Archived { get; set; }
    }
}