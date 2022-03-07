using System;
using System.Collections.Generic;
using StoreFront2.Data;

namespace StoreFront2.ViewModels
{
    public class OrderWithSomeDetailViewModel
    {
        public int Id { get; set; }
        public int SFOrderNumber { get; set; }
        public string PONumber { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public Nullable<System.DateTime> DateShipped { get; set; }
        public string TrackingNumbers { get; set; }
        public List<TrackingViewModel> TrackingData { get; set; }
        public string OrderStatus { get; set; }
        public string OrderStatusDesc { get; set; }
        public decimal TotalPrice { get; set; }
        public List<string> ProductCodes { get; set; }
        public string ProductCodeString { get; set; }
        public List<string> ShortDescs { get; set; }
        public string ShortDescString { get; set; }
        public int CustomerId { get; set; }
        public int StoreFrontId { get; set; }
        public int ShipBillType { get; set; }
        public int OnHold { get; set; }

    }
}