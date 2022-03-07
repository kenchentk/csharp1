using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class EmsOrderDetailExportModel
    {
        public int OrderDetailId { get; set; }
        public int OrderNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string PickPackCode { get; set; }
        public string Upc { get; set; }
        public int ShipWithItem { get; set; }
        public int TotalQtyOrdered { get; set; }
        public string ShortDesc { get; set; }
        public string LongDesc { get; set; }
        public string UserName { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
    }
}