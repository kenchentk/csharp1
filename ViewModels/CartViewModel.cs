using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class CartViewModel
    {
        [Key]
        public int Id { get; set; }
        public string CartId { get; set; }
        public string PONumber { get; set; }
        public string CartNote { get; set; }
        public int ProductId { get; set; }
        public string PickPackCode { get; set; }
        public string ShortDesc { get; set; }
        public decimal SellPrice { get; set; }
        public decimal SellPriceCAD { get; set; }
        public int Count { get; set; }
        public bool IsFulfilledByVendor { get; set; }
        public int VendorId { get; set; }
        public System.DateTime DateCreated { get; set; }
        public string UserId { get; set; }
        public Nullable<int> StoreFrontId { get; set; }
        public string ImageRelativePath { get; set; }
        public int DisplayOrder { get; set; }
        
        [Display(Name = "Ship Method")]
        public int ShipMethodId { get; set; }

        public int UserAddressId { get; set; }

        [Display(Name = "Address Alias")]
        public string AddressAlias { get; set; }

        [DisplayName("Company")]
        public string Company { get; set; }

        [DisplayName("Company Alias")]
        public string CompanyAlias { get; set; }

        [DisplayName("Alias")]
        public string Alias { get; set; }

        [DisplayName("First Name")]
        public string FirstName { get; set; }

        [DisplayName("Last Name")]
        public string LastName { get; set; }

        [DisplayName("Address Line 1")]
        public string Address1 { get; set; }

        [DisplayName("Address Line 2")]
        public string Address2 { get; set; }

        public string City { get; set; }

        [DisplayName("State / Province")]
        public string State { get; set; }

        public string Zip { get; set; }

        [UIHint("CountryTemplate")]
        public string Country { get; set; }

        public string Phone { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        public bool SetAsDefaultShipTo { get; set; }

    }
}
