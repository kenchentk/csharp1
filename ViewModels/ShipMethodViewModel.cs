using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class ShipMethodViewModel
    {
        public int Id { get; set; }
        public int CarrierId { get; set; }
        public string MethodName { get; set; }
        public string Code { get; set; }
        public Nullable<int> Domestic { get; set; }
        public double MaxWeight { get; set; }
        public int Enabled { get; set; }
    }
}