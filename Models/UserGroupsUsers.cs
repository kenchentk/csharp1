using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class UserGroupsUsers
    {
        public int Id { get; set; }
        public int UserGroupId { get; set; }
        public string AspNetUserId { get; set; }
        public decimal PriceLimit { get; set; }
        public Nullable<System.DateTime> TimeRefresh { get; set; }
        public string Active { get; set; }
    }
}
