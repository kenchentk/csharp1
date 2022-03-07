using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class CategoryViewModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Category Name")]
        public string Name { get; set; }

        [Required]
        [DisplayName("Description")]
        public string Desc { get; set; }
    }
}