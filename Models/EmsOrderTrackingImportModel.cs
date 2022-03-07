using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class EmsOrderTrackingImportModel
    {
        public int Id { get; set; }
        public string TrackingNumber { get; set; }
        public int OrderNumber { get; set; }
        public string AltOrderNumber { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public int Carrier { get; set; }
        public Nullable<System.DateTime> EnterDate { get; set; }
        public int ShipMethod { get; set; }
        public int Exported { get; set; }
        public double PublishedRate { get; set; }
        public double AdjustedRate { get; set; }
        public int NotifySent { get; set; }
        public Nullable<System.DateTime> NotifyDate { get; set; }
        public int ShipWeight { get; set; }
        public Nullable<int> DeliveryStatus { get; set; }

    }

    public class EmsTrackingUpload
    {
        public string OrderId { get; set; }
        public string OrderStatus { get; set; }
        public List<EmsTracking> TrackingNumbers { get; set; }
    }

    public class EmsTracking
    {
        public string TrackingNumber { get; set; }
        public string Carrier { get; set; }
        public string Method { get; set; }
        public DateTime DateCreated { get; set; }
    }

}