using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class EmsProductImportModel
    {
        public int Id { get; set; }
        public string SFProductCode { get; set; }
        public int AvailableQty { get; set; }
    }
}