using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class UserGroups
    {
        public int Id { get; set; }
        public int StoreFrontId { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public decimal PriceLimit { get; set; }
        public Nullable<System.DateTime> TimeRefresh { get; set; }

    }
}
