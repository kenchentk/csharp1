using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class EmsOrderShipmentImportModel
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public int OrderDetailId { get; set; }
        public int QtyShipped { get; set; }
        public int OrderNumber { get; set; }
        public int ProductId { get; set; }

        public List<String> OrderTrackings { get; set; }
    }
}