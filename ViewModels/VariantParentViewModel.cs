using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class VariantParentViewModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StoreFrontId { get; set; }

        [Required]
        [DisplayName("Variant Parent Name")]
        public string VariantParentName { get; set; }
    }

}