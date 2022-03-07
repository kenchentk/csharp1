using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class SearchOrderModel
    {
        public int SFOrderNumber { get; set; }
        public string OrderStatus { get; set; }
        public string ProductCode { get; set; }
        public string ShortDesc { get; set; }
        public DateTime BeginOrderDate { get; set; }
        public DateTime EndOrderDate { get; set; }
    }
}