using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class ShipmentViewModel
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public DateTime DateShipped { get; set; }
        public int TrackId { get; set; }
        public string TrackingNumber { get; set; }
        public decimal Total { get; set; }
        public string Carrier { get; set; }
        public int Status { get; set; }
        public string UserName { get; set; }

        public List<ShipmentDetailViewModel> ShipmentDetails { get; set; }
    }
}