using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StoreFront2.ViewModels
{
    public class OrderViewModel
    {
        [Key]
        public int Id { get; set; }
        
        [HiddenInput(DisplayValue = false)]
        public int StoreFrontId { get; set; }
        
        public int SFOrderNumber { get; set; }

        public string PONumber { get; set; }

        public string UserId { get; set; }
        
        public string UserName { get; set; }
        
        [HiddenInput(DisplayValue = false)] 
        public int CustomerId { get; set; }
        
        public Nullable<System.DateTime> DateCreated { get; set; }
        
        public Nullable<System.DateTime> DateShipped { get; set; }
        
        [Required]
        [DisplayName("Tracking Number")]
        public string TrackingNumbers { get; set; }
        public string ProductIds { get; set; }
        public string ProductQtys { get; set; }

        public List<SelectListItem> TrackingNumberData { get; set; }
        
        public string OrderStatus { get; set; }
        
        public string OrderStatusDesc { get; set; }
        
        public Nullable<int> ShipType { get; set; }
        
        public string ShipAccount { get; set; }

        [HiddenInput(DisplayValue = false)] 
        public int ShipBillType { get; set; }

        [HiddenInput(DisplayValue = false)] 
        public int OnHold { get; set; }
        public string CustomShipMessage { get; set; }
        public string OrderReference1 { get; set; }
        public string OrderReference2 { get; set; }
        public string OrderReference3 { get; set; }
        public string OrderReference4 { get; set; }

        public string Company { get; set; }
        public string CompanyAlias { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Email { get; set; }

        [Required]
        [DisplayName("Ship Method")]
        public string ShipMethodCode { get; set; }

        [HiddenInput(DisplayValue = false)] 
        public decimal TotalPrice { get; set; }

    }
}