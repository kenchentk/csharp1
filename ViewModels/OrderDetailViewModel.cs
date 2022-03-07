using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class OrderDetailViewModel
    {
        [Key]
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int SFOrderNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ShortDesc { get; set; }
        public string LongDesc { get; set; }

        [DisplayName("Quantity Ordered")]
        public int Qty { get; set; }

        [DisplayName("Quantity Shipped")]
        public int QtyShipped { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public string DateCreated { get; set; }
        public string ImageRelativePath { get; set; }

        public int EnableMinQty { get; set; }
        public int MinOrder { get; set; }
        public int EnableMaxQty { get; set; }
        public int MaxOrder { get; set; }
        public bool Selected { get; set; }

        public List<ShipmentViewModel> OrderShipments { get; set; }

    }
}