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
    public class UserAddressViewModel
    {
        [Key]
        public int Id { get; set; }

        [HiddenInput]
        public int StoreFrontId { get; set; }

        [HiddenInput]
        [JsonIgnore]
        public string AspNetUserId { get; set; }

        [HiddenInput]
        [JsonIgnore]
        public string UserName { get; set; }

        [Required]
        [Display(Name = "Address Alias")]
        public string AddressAlias { get; set; }

        [DisplayName("Company")]
        public string Company { get; set; }

        [DisplayName("Company Alias")]
        public string CompanyAlias { get; set; }

        [Required]
        [DisplayName("First Name")]
        public string FirstName { get; set; }

        [Required]
        [DisplayName("Last Name")]
        public string LastName { get; set; }

        [Required]
        [DisplayName("Address Line 1")]
        public string Address1 { get; set; }

        [DisplayName("Address Line 2")]
        public string Address2 { get; set; }

        [Required]
        public string City { get; set; }

        [Required]
        [DisplayName("State / Province")]
        public string State { get; set; }

        [Required]
        public string Zip { get; set; }

        [Required]
        [UIHint("CountryTemplate")]
        public string Country { get; set; }

        public string Phone { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [UIHint("CheckBoxTemplate")]
        [Display(Name = "Default Ship To")]
        public bool DefaultShipTo { get; set; } = false;

    }
}