using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class TrackingDetailViewModel
    {
        public int Id { get; set; }
        public int OrderTrackingId { get; set; }
        public int OrderDetailId { get; set; }
        public string TrackingNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public int Qty { get; set; }
    }
}