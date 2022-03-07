using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StoreFront2.Models
{
    public class EmsOrderCustomer
    {
        [Key]
        public int Id { get; set; }

        [HiddenInput]
        public string AspNetUserId { get; set; }

        //[Required]
        [Display(Name = "User Role")]
        public string UserRole { get; set; }

        [Required]
        [Display(Name = "User Name")]
        public string UserName { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        //[Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [DisplayName("Company")]
        public string Company { get; set; }

        [DisplayName("Company Alias")]
        public string CompanyAlias { get; set; }

        [DisplayName("First Name")]
        public string FirstName { get; set; }

        [DisplayName("Last Name")]
        public string LastName { get; set; }

        [DisplayName("Address Line 1")]
        public string Address1 { get; set; }

        [DisplayName("Address Line 2")]
        public string Address2 { get; set; }

        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }

        [DisplayName("Access Restricted")]
        public bool AccessRestricted { get; set; }

        public bool Status { get; set; }

        public int StoreFrontId { get; set; }

        [DisplayName("Facility Id")]
        public int FacilityId { get; set; }

        [DisplayName("Orders are placed")]
        public bool AlertOrderReceived { get; set; }

        [DisplayName("Orders are shipped")]
        public bool AlertOrderShipped { get; set; }

        [DisplayName("Place New Order On Hold")]
        public bool OnHold { get; set; }
    }
}