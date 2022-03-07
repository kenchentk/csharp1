using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class ShipCarrierViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Enabled { get; set; }

        public List<ShipMethodViewModel> ShipMethods { get; set; }
    }
}