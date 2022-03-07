using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class EmsOrderExportModel
    {
        public int Id { get; set; }
        public int StoreFrontOrder { get; set; }
        public int UserId { get; set; }
        public int CustomerId { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public string OrderStatus { get; set; }
        public Nullable<int> OrderUrgency { get; set; }
        public string PONumber { get; set; }
        public string ShipAccount { get; set; }
        public int ShipBillType { get; set; }
        public int OnHold { get; set; }
        public Nullable<System.DateTime> FutureReleaseDate { get; set; }
        public int Exported { get; set; }
        public string CustomShipMessage { get; set; }
        public string OrderReference1 { get; set; }
        public string OrderReference2 { get; set; }
        public string OrderReference3 { get; set; }
        public string OrderReference4 { get; set; }

        public List<EmsOrderDetailExportModel> OrderDetails { get; set; }
        public AspNetUser Customer { get; set; }
        public EmsOrderShipToExportModel OrderShipTo { get; set; }
        public ShipMethod ShipMethod { get; set; }
    }
}