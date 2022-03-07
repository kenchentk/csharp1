using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class VariantDetailViewModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ParentId { get; set; }

        [Required]
        [DisplayName("Variant Name")]
        public string VariantDetailName { get; set; }
    }
}
