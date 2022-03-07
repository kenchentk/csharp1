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
    public class ProductViewModel
    {
        [Key]
        public int Id { get; set; }

        [DisplayName("Product Id")]
        public int EmsProductId { get; set; }

        [Required]
        [DisplayName("Product Code")]
        public string ProductCode { get; set; }

        [DisplayName("Pick Pack Code")]
        public string PickPackCode { get; set; }

        [DisplayName("UPC")]
        public string Upc { get; set; }

        [Required]
        [DataType(DataType.MultilineText)]
        [StringLength(250)]
        [DisplayName("Short Description")]
        public string ShortDesc { get; set; }

        [Required]
        [DataType(DataType.MultilineText)]
        [StringLength(1000)]
        [DisplayName("Long Description")]
        public string LongDesc { get; set; }
        public double Weight { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Restricted { get; set; }
        public double DefaultValue { get; set; }

        [DisplayName("Selling Price")]
        public decimal SellPrice { get; set; }
        
        [DisplayName("CAD Price ")]
        public decimal SellPriceCAD { get; set; }
        public int LowLevel { get; set; }

        //[Required(AllowEmptyStrings = false)]
        [DisplayName("Unit of Measure")]
        public string Uom { get; set; }

        [HiddenInput]
        public string CreatedBy { get; set; }

        [HiddenInput]
        public DateTime DateCreated { get; set; }

        [HiddenInput]
        public string UserId { get; set; }

        [HiddenInput]
        public string UserName { get; set; }

        public Nullable<double> ItemValue { get; set; }
        [DisplayName("Enable")]
        public bool EnableMinQty { get; set; }
        [DisplayName("Minimum Order Quantity")]
        public int MinQty { get; set; }
        [DisplayName("Enable")]
        public bool EnableMaxQty { get; set; }
        [DisplayName("Maximum Order Quantity")]
        public int MaxQty { get; set; }
        public int OrderQty { get; set; }
        public int DisplayOrder { get; set; }
        public short Status { get; set; }

        [DisplayName("Special")]
        public bool? IsSpecial { get; set; }

        [DisplayName("Vendor Fulfilled")]
        public bool IsFulfilledByVendor { get; set; }

        [DisplayName("Vendor")]
        public int VendorId { get; set; }

        [DisplayName("Available Inventory")]
        public int EMSQty { get; set; }
        public bool InStock { get; set; }

        [DisplayName("Estimated Restocking Date")]
        public DateTime? EstRestockDate { get; set; }

        [DisplayName("Image")]
        public string ImageRelativePath { get; set; }
        public List<ProductImageVM> ProductImages { get; set; }
        [DisplayName("Files")]
        public string FileRelativePath { get; set; }
        public List<ProductFileVM> ProductFiles { get; set; }
        public List<ProductCategoriesVM> ProductCategories { get; set; }
        public List<ProductVariantsVM> ProductVariants { get; set; }
        public List<CategoryViewModel> Categories { get; set; }    
        //public List<VariantParentViewModel> VariantParents { get; set; }
        public List<string> CategoriesList { get; set; }
        public string CategoriesString { get; set; }
        public string CategoriesDescString { get; set; }

        public int VariantParent1Id { get; set; }
        public int VariantParent2Id { get; set; }
        public int VariantParent3Id { get; set; }
        public int VariantDetail1Id { get; set; }
        public int VariantDetail2Id { get; set; }
        public int VariantDetail3Id { get; set; }
        public string VariantDetail1Name { get; set; }
        public string VariantDetail2Name { get; set; }
        public string VariantDetail3Name { get; set; }

    }

    public class ProductImageVM
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public DateTime DateCreated { get; set; }
        public string RelativePath { get; set; }
        public string FileName { get; set; }
        public string UserName { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ProductFileVM
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public DateTime DateCreated { get; set; }
        public string RelativePath { get; set; }
        public string FileIcon { get; set; }
        public string UserName { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ProductCategoriesVM
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }       
    }
    
    public class ProductVariantsVM
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Parent1 { get; set; }
        public int Parent2 { get; set; }
        public int Parent3 { get; set; }  
        public int Variant1 { get; set; }
        public int Variant2 { get; set; }
        public int Variant3 { get; set; }
        public string Variant1Name { get; set; }
        public string Variant2Name { get; set; }
        public string Variant3Name { get; set; }
    }
}