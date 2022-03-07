using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace StoreFront2.ViewModels
{
    public class VendorViewModel
    {
        [Key]
        public int Id { get; set; }

        [HiddenInput]
        public int StoreFrontId { get; set; }

        [HiddenInput]
        [DisplayName("Storefront User")]
        public string AspNetUserId { get; set; }

        public int AspNetUserSfId { get; set; }

        public string UserName { get; set; }

        [Required]
        [Display(Name = "Alias")]
        public string Alias { get; set; }

        //[Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [DisplayName("Vendor Name")]
        public string Company { get; set; }

        [Required]
        [DisplayName("Vendor Company Alias")]
        public string CompanyAlias { get; set; }

        [Required]
        [DisplayName("Address Line 1")]
        public string Address1 { get; set; }

        [DisplayName("Address Line 2")]
        public string Address2 { get; set; }

        [Required]
        public string City { get; set; }

        [Required]
        [DisplayName("State/Province")]
        public string State { get; set; }

        [Required]
        [DisplayName("Zip/Postal Code")]
        public string Zip { get; set; }

        [Required]
        [UIHint("CountryTemplate")]
        public string Country { get; set; }

        public string Phone { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [UIHint("CheckBoxTemplate")]
        [Display(Name = "Active")]
        public bool Status { get; set; } = false;

    }
}