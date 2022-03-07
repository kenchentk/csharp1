using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class Site
    {
        public int StoreFrontId { get; set; }
        public string AspNetUserId { get; set; }
        public bool AdminAsShopper { get; set; }
        public bool IsVendor { get; set; }
        public bool IsPunchOutUser { get; set; }
        public int CurrencyFlag { get; set; }
        public string BuyerCookie { get; set; }
        public string Token { get; set; }
        public DateTime TokenExpireTime { get; set; }
        public string StoreFrontName { get; internal set; }
        public string LayoutPath { get; internal set; }
        public string StylePath { get; set; }
        public string SiteIcon { get; internal set; }
        public string SiteTitle { get; internal set; }
        public string SiteFooter { get; internal set; }
        public UserPermission SiteAuth { get; set; }
        public UserSetting SiteUserSetting { get; set; }
        public SystemSetting Setting { get; set; }

    }
}