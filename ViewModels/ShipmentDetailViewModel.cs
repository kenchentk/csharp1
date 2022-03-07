using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class ShipmentDetailViewModel
    {
        public int Id { get; set; }
        public int OrderShipmentId { get; set; }
        public int OrderDetailId { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string PickPackCode { get; set; }
        public string ShortDesc { get; set; }
        public int Qty { get; set; }
        public int TrackId { get; set; }
        public string UserName { get; set; }

        public List<TrackingViewModel> Trackings { get; set; }
    }
}