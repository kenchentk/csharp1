using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StoreFront2.Data;
using System.Web.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace StoreFront2.ViewModels
{
    public class StoreFrontViewModel
    {
        public int Id { get; set; }

        [Key]
        public int IdKey { get; set; }

        [Required]
        public string Name { get; set; }

        [DisplayName("Customer Service Rep")]
        public string CustomerServiceRep { get; set; }

        [DisplayName("Office Number")]
        public string OfficeNumber { get; set; }

        [DisplayName("Office Hours")]
        public string OfficeHours { get; set; }

        [Required]
        [DisplayName("Main Email")]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        [DisplayName("Base Url")]
        [DataType(DataType.Url)]
        public string BaseUrl { get; set; }

        [DisplayName("Main Layout Path")]
        public string LayoutPath { get; set; }

        [DisplayName("Main Style Path")]
        public string StylePath { get; internal set; }

        [DisplayName("Website Icon")]
        public string SiteIcon { get; set; }

        [DisplayName("Website Title")]
        public string SiteTitle { get; set; }

        [DisplayName("Login Image Path")]
        public string LoginImage { get; set; }

        [DisplayName("Website Footer")]
        public string SiteFooter { get; set; }

        [DisplayName("Template Id")]
        public int? TemplateId { get; set; }

        public IEnumerable<SelectListItem> StoreFronts { get; set; }
    }
}