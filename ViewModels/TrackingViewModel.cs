using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class TrackingViewModel
    {
        public int Id { get; set; }
        public string TrackingNumber { get; set; }
        public DateTime DateCreated { get; set; }
        public string Carrier { get; set; }
        public Nullable<System.DateTime> EnterDate { get; set; }
        public string ShipMethod { get; set; }
        public double? PublishedRate { get; set; }
        public double? AdjustedRate { get; set; }
        public int? ShipWeight { get; set; }
        public Nullable<int> DeliveryStatus { get; set; }
        public List<TrackingDetailViewModel> TrackingDetails { get; set; }
    }
}