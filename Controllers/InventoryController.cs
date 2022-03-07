using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using StoreFront2.Data;
using StoreFront2.Models;
using StoreFront2.Helpers;
using StoreFront2.ViewModels;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;

namespace StoreFront2.Controllers
{
    public class InventoryController : Controller
    {
        private StoreFront2Entities _sfDb = new StoreFront2Entities();

        private Site _site = new Site();

        private string _userName = "";
        private AspNetUser _userSf;
        private IQueryable<int> _sfIdList;
        private List<string> _userRoleList;

        private ApplicationUserManager _userManager;

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        protected override void Initialize(RequestContext requestContext)
        {
            base.Initialize(requestContext);

            if (Request != null)
            {
                // Check the requesting Url
                //var request = System.Web.HttpContext.Current.Request;
                string baseUrl = new Uri(Request.Url, Url.Content("~")).AbsoluteUri;

                // Restore or create Site values
                StoreFront sf = _sfDb.StoreFronts.Where(s => s.BaseUrl == baseUrl).FirstOrDefault();
                if (sf == null) sf = _sfDb.StoreFronts.Where(s => s.StoreFrontName == "Ems").FirstOrDefault();
                _site = (Site)Session["Site"];
                if (_site == null)
                {
                    _site = new Site();
                    _site.StoreFrontId = sf.Id;
                    _site.StoreFrontName = sf.StoreFrontName;
                    _site.AdminAsShopper = false;
                    _site.LayoutPath = sf.LayoutPath;
                    _site.StylePath = sf.StylePath;
                    _site.SiteIcon = sf.SiteIcon;
                    _site.SiteTitle = sf.SiteTitle;
                    _site.SiteFooter = sf.SiteFooter;
                    _site.SiteAuth = new UserPermission();
                    _site.SiteUserSetting = new UserSetting();

                }
                _site.Setting = _sfDb.SystemSettings.Where(s => s.StoreFrontId == sf.Id).FirstOrDefault();

                // Retrieve this logged in user record
                _userName = User.Identity.Name;
                if (_userName != null && _userName.Length > 0)
                {
                    _userSf = _sfDb.AspNetUsers.Where(u => u.UserName == _userName).FirstOrDefault();

                    _sfIdList = from usf in _sfDb.UserStoreFronts
                                where usf.AspNetUserId == _userSf.Id
                                select usf.StoreFrontId;
                    _userRoleList = _sfDb.AspNetRoles.Where(t => t.AspNetUsers.Any(u => u.UserName == _userName)).Select(r => r.Name).ToList();

                    // Retrieve existing cart items
                    List<Cart> existingCartItems = (from c in _sfDb.Carts
                                                    where c.UserId == _userSf.Id && c.StoreFrontId == _site.StoreFrontId && c.Count > 0
                                                    select c).ToList();
                    Session["Cart"] = existingCartItems;
                    ViewBag.CartItemCount = existingCartItems.Count();
                    Session["CartItemCount"] = ViewBag.CartItemCount;

                    // requery permissions
                    var query = (from up in _sfDb.UserPermissions
                                 where up.AspNetUserId == _userSf.Id && up.StoreFrontId == _site.StoreFrontId
                                 select up).FirstOrDefault();
                    if (query == null)
                    {
                        query = new UserPermission()
                        {
                            AdminUserModify = 0,
                            AdminSettingModify = 0,
                            InventoryItemModify = 0,
                            InventoryRestrictCategory = 0,
                            InventoryCategoryModify = 0,
                            OrderRestrictShipMethod = 0,
                            OrderCreate = 0,
                            OrderCancel = 0,
                        };
                    }
                    _site.SiteAuth = query;

                    // query user setting
                    UserSetting userSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == _userSf.Id && us.StoreFrontId == sf.Id).FirstOrDefault();
                    if (userSetting == null) userSetting = new UserSetting() { BudgetCurrentTotal = 0, BudgetIgnore = 0, BudgetLimit = 0, BudgetResetInterval = 0, BudgetLastResetDate = new DateTime(1, 1, 1), BudgetNextResetDate = new DateTime(1, 1, 1) };

                    _site.SiteUserSetting = userSetting;

                    _site.IsVendor = _userSf.IsVendor == 1;
                    _site.IsPunchOutUser = _userSf.IsPunchOutUser == 1;

                }

                Session["Site"] = _site;

            }
        }

        [DataContract]
        public class ChunkMetaData
        {
            [DataMember(Name = "uploadUid")]
            public string UploadUid { get; set; }
            [DataMember(Name = "fileName")]
            public string FileName { get; set; }
            [DataMember(Name = "relativePath")]
            public string RelativePath { get; set; }
            [DataMember(Name = "contentType")]
            public string ContentType { get; set; }
            [DataMember(Name = "chunkIndex")]
            public long ChunkIndex { get; set; }
            [DataMember(Name = "totalChunks")]
            public long TotalChunks { get; set; }
            [DataMember(Name = "totalFileSize")]
            public long TotalFileSize { get; set; }
        }

        public class FileResult
        {
            public bool uploaded { get; set; }
            public string fileUid { get; set; }
        }

        public void AppendToFile(string fullPath, Stream content)
        {
            try
            {
                using (FileStream stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    using (content)
                    {
                        content.CopyTo(stream);
                    }
                }
            }
            catch (IOException ex)
            {
                throw ex;
            }
        }

        // (Not used currently)
        public ActionResult Chunk_Upload_Save(IEnumerable<HttpPostedFileBase> files, string metaData)
        {
            if (metaData == null)
            {
                return Chunk_Upload_Async_Save(files);
            }

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(metaData));
            var serializer = new DataContractJsonSerializer(typeof(ChunkMetaData));
            ChunkMetaData chunkData = serializer.ReadObject(ms) as ChunkMetaData;
            string path = String.Empty;
            // The Name of the Upload component is "files"
            if (files != null)
            {
                foreach (var file in files)
                {
                    path = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Images"), chunkData.FileName);

                    AppendToFile(path, file.InputStream);
                }
            }

            FileResult fileBlob = new FileResult();
            fileBlob.uploaded = chunkData.TotalChunks - 1 <= chunkData.ChunkIndex;
            fileBlob.fileUid = chunkData.UploadUid;

            return Json(fileBlob);
        }

        // (Not used currently)
        public ActionResult Chunk_Upload_Remove(string[] fileNames)
        {
            // The parameter of the Remove action must be called "fileNames"

            if (fileNames != null)
            {
                foreach (var fullName in fileNames)
                {
                    var fileName = Path.GetFileName(fullName);
                    var physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Images"), fileName);

                    // TODO: Verify user permissions

                    if (System.IO.File.Exists(physicalPath))
                    {
                        // The files are not actually removed in this demo
                        System.IO.File.Delete(physicalPath);
                    }
                }
            }

            // Return an empty string to signify success
            return Content("");
        }

        // (Not used currently)
        public ActionResult Chunk_Upload_Async_Save(IEnumerable<HttpPostedFileBase> files)
        {
            // The Name of the Upload component is "files"
            if (files != null)
            {
                foreach (var file in files)
                {
                    // Some browsers send file names with full path.
                    // We are only interested in the file name.
                    var fileName = Path.GetFileName(file.FileName);
                    var physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Images"), fileName);

                    // The files are not actually saved in this demo
                    file.SaveAs(physicalPath);
                }
            }

            // Return an empty string to signify success
            return Content("");
        }

        [TokenAuthorize]
        public ActionResult Read_Categories(int productId)
        {
            try
            {
                List<ProductCategoriesVM> productcategories = new List<ProductCategoriesVM>();

                productcategories = _sfDb.ProductCategories.Where(pc => pc.ProductId == productId)
                            .Select(pc => new ProductCategoriesVM
                            {
                                Id = pc.Id,
                                ProductId = pc.ProductId,
                                CategoryId = pc.CategoryId,
                                Name = _sfDb.Categories.Where(c => c.Id == pc.CategoryId).FirstOrDefault().Name ?? "",
                                Desc = _sfDb.Categories.Where(c => c.Id == pc.CategoryId).FirstOrDefault().Desc ?? "",
                            })
                            .OrderBy(pc => pc.Name).ToList();

                return Json(productcategories, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Remove_Categories(int ProductCategoryId)
        {
            try
            {
                ProductCategory selectProductCategory;

                selectProductCategory = (from pc in _sfDb.ProductCategories
                                            where pc.Id == ProductCategoryId
                                            select pc).FirstOrDefault();
              
                if (selectProductCategory != null)
                {
                    _sfDb.ProductCategories.Remove(selectProductCategory);
                    _sfDb.SaveChanges();
                }
                // Return an empty string to signify success
                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        //public ActionResult Async_Add_Categories(int ProductId, int CategoryId)
        //{
        //    try
        //    {
        //        ProductCategory selectProductCategory;
        //        selectProductCategory = (from pc in _sfDb.ProductCategories
        //                                 where pc.ProductId == ProductId && pc.CategoryId == CategoryId
        //                                 select pc).FirstOrDefault();
        //        if (selectProductCategory == null)
        //        {
        //            selectProductCategory = new ProductCategory();
        //            selectProductCategory.ProductId = ProductId;
        //            selectProductCategory.CategoryId = CategoryId;
        //            _sfDb.ProductCategories.Add(selectProductCategory);
        //            _sfDb.SaveChanges();
        //        }
        //        // Return an empty string to signify success
        //        return Json(new { result = "success" }, "text/plain");
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { result = "Error", message = ex.Message });
        //    }
        //}
        public ActionResult Async_Add_Categories(int ProductId, int CategoryId)
        {
            try
            {
                ProductCategory selectProductCategory;

                selectProductCategory = (from pc in _sfDb.ProductCategories
                                         where pc.ProductId == ProductId && pc.CategoryId == CategoryId
                                         select pc).FirstOrDefault();

                if (selectProductCategory == null)
                {
                    selectProductCategory = new ProductCategory();
                    selectProductCategory.ProductId = ProductId;
                    selectProductCategory.CategoryId = CategoryId;
                    _sfDb.ProductCategories.Add(selectProductCategory);
                    _sfDb.SaveChanges();
                }
                // Return an empty string to signify success
                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Read_UploadedImages(int productId) 
        {
            try
            {
                List<ProductImageVM> uploadedImages = new List<ProductImageVM>();

                // load everything
                uploadedImages = _sfDb.ProductImages.Where(pi => pi.ProductId == productId)
                            .Select(pi => new ProductImageVM
                            {
                                Id = pi.Id,
                                ProductId = pi.ProductId,
                                DateCreated = pi.DateCreated,
                                UserName = pi.UserName,
                                RelativePath = pi.RelativePath,
                                FileName = pi.FileName,
                                DisplayOrder = pi.DisplayOrder ?? 0,
                            })
                            .OrderBy(pi => pi.DisplayOrder).ToList();

                return Json(uploadedImages, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Save(IEnumerable<HttpPostedFileBase> images, int productId)
        {
            try
            {
                string physicalPath = string.Empty;
                int selectedProductId = 0;
                int selectedProductImageId = 0;
                string selectedProductImageDateCreated = "";
                string selectedProductImageUserName = "";

                // The Name of the Upload component is "files"
                if (images != null)
                {
                    foreach (var file in images)
                    {
                        // Some browsers send file names with full path.
                        // We are only interested in the file name.
                        var fileName = Path.GetFileName(file.FileName);
                        physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Images"), fileName);

                        // Save file at the server
                        file.SaveAs(physicalPath);

                        if (productId > 0)
                        {
                            // If there's no default image (displayorder = 1) set this one as default
                            int displayOrder = 0;
                            ProductImage defaultProductImage = _sfDb.ProductImages.Where(pi => pi.ProductId == productId && (pi.DisplayOrder ?? 1) == 1).FirstOrDefault();
                            if (defaultProductImage == null) displayOrder = 1;

                            string relativePath = "Content/" + _site.StoreFrontName + "/Images/" + fileName;
                            ProductImage selectedProductImage = (from pi in _sfDb.ProductImages
                                                                 where pi.ProductId == productId && pi.RelativePath == relativePath
                                                                 select pi).FirstOrDefault();
                            if (selectedProductImage == null)
                            {
                                // If this image is not default, get the next displayorder number
                                if (displayOrder == 0) displayOrder = (_sfDb.ProductImages.Where(pi => pi.ProductId == productId).DefaultIfEmpty().Max(pi => pi.DisplayOrder) ?? 0) + 1;

                                selectedProductImage = new ProductImage();
                                selectedProductImage.ProductId = productId;
                                selectedProductImage.DateCreated = DateTime.Now;
                                selectedProductImage.UserId = _userSf.Id;
                                selectedProductImage.UserName = _userSf.UserName;
                                selectedProductImage.DisplayOrder = displayOrder;
                                selectedProductImage.RelativePath = relativePath;
                                selectedProductImage.DefaultImagePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                                selectedProductImage.FileName = fileName;

                                _sfDb.ProductImages.Add(selectedProductImage);
                            }
                            else
                            {
                                // If there's no default image (displayorder = 1) set this one as default
                                if (displayOrder == 1) selectedProductImage.DisplayOrder = 1;
                                selectedProductImage.RelativePath = relativePath;
                                _sfDb.Entry(selectedProductImage).State = EntityState.Modified;
                            }

                            _sfDb.SaveChanges();
                            selectedProductId = productId;
                            selectedProductImageId = selectedProductImage.Id;
                            selectedProductImageDateCreated = selectedProductImage.DateCreated.ToString("d");
                            selectedProductImageUserName = selectedProductImage.UserName;
                        }
                        else
                        {
                            // create new placeholder product/product image
                            Product selectedProduct = new Product() { ProductCode = "PlaceHolder", UserId = _userSf.Id, UserName = _userSf.UserName, CreatedBy = _userSf.UserName, StoreFrontId = _site.StoreFrontId };
                            List<Product> placeholderProducts = (from p in _sfDb.Products
                                                                 where p.ProductCode == "PlaceHolder" && p.UserId == _userSf.Id && p.UserName == _userSf.UserName && p.StoreFrontId == _site.StoreFrontId
                                                                 select p).ToList();
                            if (placeholderProducts.Count() > 0)
                            {
                                foreach (Product p in placeholderProducts)
                                    if (selectedProduct.Id == 0)
                                        selectedProduct = p;
                                    else
                                        _sfDb.Products.Remove(p);
                            }
                            else
                            {
                                _sfDb.Products.Add(selectedProduct);
                            }
                            _sfDb.SaveChanges();

                            productId = selectedProduct.Id;

                            // find if the image has already been saved before
                            ProductImage selectedProductImage;
                            selectedProductImage = (from pi in _sfDb.ProductImages
                                                    where pi.ProductId == productId
                                                    select pi).FirstOrDefault();
                            if (selectedProductImage == null)
                                selectedProductImage = new ProductImage();

                            selectedProductImage.ProductId = productId;
                            selectedProductImage.DateCreated = DateTime.Now;
                            selectedProductImage.UserId = _userSf.Id;
                            selectedProductImage.UserName = _userSf.UserName;
                            selectedProductImage.DisplayOrder = 1;
                            selectedProductImage.RelativePath = "Content/" + _site.StoreFrontName + "/Images/" + fileName;
                            selectedProductImage.DefaultImagePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                            selectedProductImage.FileName = fileName;

                            if (selectedProductImage.Id == 0)
                                _sfDb.ProductImages.Add(selectedProductImage);
                            else
                                _sfDb.Entry(selectedProductImage).State = EntityState.Modified;

                            _sfDb.SaveChanges();
                            selectedProductId = productId;
                            selectedProductImageId = selectedProductImage.Id;
                            selectedProductImageDateCreated = selectedProductImage.DateCreated.ToString("d");
                            selectedProductImageUserName = selectedProductImage.UserName;
                        }
                    }
                }

                // Save the uploaded file's path in the session for use later
                if (physicalPath.Length > 0)
                {
                    int startIndex = physicalPath.IndexOf("Content");
                    string imageFilePath = String.Empty;
                    imageFilePath = physicalPath.Substring(startIndex, physicalPath.Length - startIndex);
                    imageFilePath = imageFilePath.Replace("\\", "/");

                    Session["ImageFilePath"] = imageFilePath;

                    // Return file path to signify success
                    return Json(new
                    {
                        imageRelativePath = imageFilePath,
                        productId = selectedProductId,
                        productImageId = selectedProductImageId,
                        productImageDateCreated = selectedProductImageDateCreated,
                        productImageUserName = selectedProductImageUserName,
                    }, "text/plain");
                }
                else
                {
                    return Json(new { result = "Error", message = "No file selected" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_ImageReorder(List<ProductImageVM> productImageVMs)
        {
            try
            {
                if (productImageVMs != null)
                {
                    foreach (ProductImageVM pivm in productImageVMs)
                    {
                        ProductImage selectedProductImage = _sfDb.ProductImages.Where(pi => pi.FileName == pivm.FileName && pi.Id == pivm.Id && pi.ProductId == pivm.ProductId).FirstOrDefault();
                        if (selectedProductImage != null)
                        {
                            selectedProductImage.DisplayOrder = pivm.DisplayOrder;
                            _sfDb.Entry(selectedProductImage).State = EntityState.Modified;
                        }
                    }
                    _sfDb.SaveChanges();
                    return Json(new { result = "success" }, "text/plain");
                }
                // Return warning to signify no action
                return Json(new { result = "Warning", message = "No action taken" });
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Remove(ProductImageVM productImageVM)
        {
            try
            {
                // The parameter of the Remove action must be ProductImageVM
                if (productImageVM != null)
                {
                    var fileName = productImageVM.FileName;

                    if (fileName.ToLower() != "default.png")
                    {
                        //var physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Images"), fileName);

                        //// TODO: Verify user permissions
                        ///trying to do this without actually removing the image for right now to see if that is the issue
                        //if (System.IO.File.Exists(physicalPath))
                        //{
                        //    // The files are not actually removed in this demo
                        //    System.IO.File.Delete(physicalPath);
                        //}

                        ProductImage selectedProductImage;
                        if (productImageVM.ProductId > 0)
                        {
                            selectedProductImage = (from pi in _sfDb.ProductImages
                                                    where pi.ProductId == productImageVM.ProductId && pi.Id == productImageVM.Id
                                                    select pi).FirstOrDefault();
                        }
                        else
                        {
                            selectedProductImage = (from pi in _sfDb.ProductImages
                                                    where pi.FileName == productImageVM.FileName && pi.Id == productImageVM.Id
                                                    select pi).FirstOrDefault();
                        }

                        if (selectedProductImage != null)
                        {
                            _sfDb.ProductImages.Remove(selectedProductImage);
                            _sfDb.SaveChanges();

                            // Reorder 
                            List<ProductImage> newProductImageDisplayOrder = _sfDb.ProductImages.Where(pi => pi.ProductId == productImageVM.ProductId).OrderBy(pi => pi.DisplayOrder).ToList();
                            for (int i = 0; i < newProductImageDisplayOrder.Count(); i++)
                            {
                                newProductImageDisplayOrder[i].DisplayOrder = i + 1;
                                _sfDb.Entry(newProductImageDisplayOrder[i]).State = EntityState.Modified;
                            }
                            _sfDb.SaveChanges();                    

                        }


                    }

                }

                // Return an empty string to signify success
                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Save_Files(IEnumerable<HttpPostedFileBase> files, int productId)
        {
            try
            {
                string physicalPath = string.Empty;
                int selectedProductId = 0;
                int selectedProductFileId = 0;
                string selectedProductFileDateCreated = "";
                string selectedProductFileUserName = "";

                // The Name of the Upload component is "files"
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        // Some browsers send file names with full path.
                        // We are only interested in the file name.
                        var fileName = Path.GetFileName(file.FileName);
                        physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Files"), fileName);

                        // The files are not actually saved in this demo
                        file.SaveAs(physicalPath);

                        if (productId != null && productId > 0)
                        {
                            ProductFile selectedProductFile = (from pi in _sfDb.ProductFiles
                                                               where pi.ProductId == productId
                                                               select pi).FirstOrDefault();
                            if (selectedProductFile == null)
                            {
                                selectedProductFile = new ProductFile();
                                selectedProductFile.ProductId = productId;
                                selectedProductFile.DateCreated = DateTime.Now;
                                selectedProductFile.UserId = _userSf.Id;
                                selectedProductFile.UserName = _userSf.UserName;
                                selectedProductFile.RelativePath = "Content/" + _site.StoreFrontName + "/Files/" + fileName;
                                selectedProductFile.FileName = "";

                                _sfDb.ProductFiles.Add(selectedProductFile);
                            }
                            else
                            {
                                selectedProductFile.RelativePath = "Content/" + _site.StoreFrontName + "/Images/" + Path.GetFileName(physicalPath);
                                _sfDb.Entry(selectedProductFile).State = EntityState.Modified;
                            }

                            _sfDb.SaveChanges();
                            selectedProductId = productId;
                            selectedProductFileId = selectedProductFile.Id;
                            selectedProductFileDateCreated = selectedProductFile.DateCreated.ToString("d");
                            selectedProductFileUserName = selectedProductFile.UserName;
                        }
                        else
                        {
                            // create new placeholder product/product file
                            Product selectedProduct = new Product() { ProductCode = "PlaceHolder", UserId = _userSf.Id, UserName = _userSf.UserName, CreatedBy = _userSf.UserName, StoreFrontId = _site.StoreFrontId };
                            List<Product> placeholderProducts = (from p in _sfDb.Products
                                                                 where p.ProductCode == "PlaceHolder" && p.UserId == _userSf.Id && p.UserName == _userSf.UserName
                                                                 select p).ToList();
                            if (placeholderProducts.Count() > 0)
                            {
                                foreach (Product p in placeholderProducts)
                                    if (selectedProduct.Id == 0)
                                        selectedProduct = p;
                                    else
                                        _sfDb.Products.Remove(p);
                            }
                            else
                            {
                                _sfDb.Products.Add(selectedProduct);
                            }
                            _sfDb.SaveChanges();

                            productId = selectedProduct.Id;

                            // find if the file has already been saved before
                            ProductFile selectedProducFile;
                            selectedProducFile = (from pi in _sfDb.ProductFiles
                                                  where pi.ProductId == productId
                                                  select pi).FirstOrDefault();
                            if (selectedProducFile == null)
                                selectedProducFile = new ProductFile();

                            selectedProducFile.ProductId = productId;
                            selectedProducFile.DateCreated = DateTime.Now;
                            selectedProducFile.UserId = _userSf.Id;
                            selectedProducFile.UserName = _userSf.UserName;
                            selectedProducFile.RelativePath = "Content/" + _site.StoreFrontName + "/Files/" + fileName;
                            selectedProducFile.FileName = fileName;

                            if (selectedProducFile.Id == 0)
                                _sfDb.ProductFiles.Add(selectedProducFile);
                            else
                                _sfDb.Entry(selectedProducFile).State = EntityState.Modified;

                            _sfDb.SaveChanges();
                            selectedProductId = productId;
                            selectedProductFileId = selectedProducFile.Id;
                            selectedProductFileDateCreated = selectedProducFile.DateCreated.ToString("d");
                            selectedProductFileUserName = selectedProducFile.UserName;
                        }

                    }
                }

                if (physicalPath.Length > 0)
                {

                    // Save the uploaded file's path in the session for use later
                    int startIndex = physicalPath.IndexOf("Content");
                    string downloadFilePath = String.Empty;
                    downloadFilePath = physicalPath.Substring(startIndex, physicalPath.Length - startIndex);
                    downloadFilePath = downloadFilePath.Replace("\\", "/");

                    Session["DownloadFilePath"] = downloadFilePath;
                    // Return file path to signify success
                    return Json(new
                    {
                        fileRelativePath = downloadFilePath,
                        productId = selectedProductId,
                        productFileId = selectedProductFileId,
                        productFileDateCreated = selectedProductFileDateCreated,
                        productFileUserName = selectedProductFileUserName,
                    }, "text/plain");
                }
                else
                {
                    return Json(new { result = "Error", message = "No file selected" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Remove_Files(string[] fileNames, int imageId, int productId)
        {
            try
            {
                // The parameter of the Remove action must be called "fileNames"
                if (fileNames != null)
                {
                    foreach (var fullName in fileNames)
                    {
                        var fileName = Path.GetFileName(fullName);

                        if (fileName.ToLower() == "default.png")
                            continue;

                        var physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Files"), fileName);

                        // TODO: Verify user permissions

                        if (System.IO.File.Exists(physicalPath))
                        {
                            // The files are not actually removed in this demo
                            System.IO.File.Delete(physicalPath);

                            ProductFile selectedProductFile;
                            if (productId > 0)
                            {
                                selectedProductFile = (from pi in _sfDb.ProductFiles
                                                       where pi.ProductId == productId
                                                       select pi).FirstOrDefault();
                            }
                            else
                            {
                                selectedProductFile = (from pi in _sfDb.ProductFiles
                                                       where pi.FileName == fileName
                                                       select pi).FirstOrDefault();
                            }

                            if (selectedProductFile != null)
                            {
                                _sfDb.ProductFiles.Remove(selectedProductFile);
                                _sfDb.SaveChanges();
                            }
                        }
                    }
                }

                // Return an empty string to signify success
                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        #region Stock
        [TokenAuthorize]
        public ActionResult Stock(FilterViewModel paramFilter)
        {
            try
            {
                if (paramFilter == null)
                {
                    paramFilter = new FilterViewModel()
                    {
                        Status = "Active"
                    };
                }
                return View(paramFilter);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }
        }

        [TokenAuthorize]
        public ActionResult Products_Read([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                var productList = (from p in _sfDb.Products
                                   join pi in _sfDb.ProductImages on p.Id equals pi.ProductId into subpi
                                   from pi in subpi.DefaultIfEmpty()

                                   where p.StoreFrontId == _site.StoreFrontId && p.ProductCode.ToLower() != "placeholder" && (pi.DisplayOrder ?? 1) == 1
                                   orderby p.ProductCode
                                   select new ProductViewModel()
                                   {
                                       Id = p.Id,
                                       EmsProductId = p.EMSProductId,
                                       ProductCode = p.ProductCode,
                                       PickPackCode = p.PickPackCode,
                                       Upc = p.Upc,
                                       ShortDesc = p.ShortDesc,
                                       LongDesc = p.LongDesc,
                                       Weight = p.Weight,
                                       Length = p.Length,
                                       Width = p.Width,
                                       Height = p.Height,
                                       Restricted = p.Restricted == 1 ? true : false,
                                       DefaultValue = p.DefaultValue,
                                       SellPrice = p.SellPrice,
                                       SellPriceCAD = p.SellPriceCAD,
                                       LowLevel = p.LowLevel,
                                       Uom = p.Uom,
                                       CreatedBy = p.CreatedBy,
                                       DateCreated = p.DateCreated,
                                       UserId = p.UserId,
                                       UserName = p.UserName,
                                       ItemValue = p.ItemValue,
                                       EnableMinQty = p.EnableMinQty == 1 ? true : false,
                                       MinQty = p.MinQty,
                                       EnableMaxQty = p.EnableMaxQty == 1 ? true : false,
                                       MaxQty = p.MaxQty,
                                       EMSQty = p.EMSQty,
                                       EstRestockDate = p.EstRestockDate,
                                       Status = p.Status,
                                       ImageRelativePath = pi.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Images/default.png" : pi.RelativePath,
                                       Categories = (from c in _sfDb.Categories
                                                     join pc in _sfDb.ProductCategories on c.Id equals pc.CategoryId
                                                     where pc.ProductId == p.Id
                                                     select new CategoryViewModel { Id = c.Id, Desc = c.Desc }).ToList(),
                                   }).AsEnumerable()
                                    .Select(p => new ProductViewModel()
                                    {
                                        Id = p.Id,
                                        EmsProductId = p.EmsProductId,
                                        ProductCode = p.ProductCode,
                                        PickPackCode = p.PickPackCode,
                                        Upc = p.Upc,
                                        ShortDesc = p.ShortDesc,
                                        LongDesc = p.LongDesc,
                                        Weight = p.Weight,
                                        Length = p.Length,
                                        Width = p.Width,
                                        Height = p.Height,
                                        Restricted = p.Restricted,
                                        DefaultValue = p.DefaultValue,
                                        SellPrice = p.SellPrice,
                                        SellPriceCAD = p.SellPriceCAD,
                                        LowLevel = p.LowLevel,
                                        Uom = p.Uom,
                                        CreatedBy = p.CreatedBy,
                                        DateCreated = p.DateCreated,
                                        UserId = p.UserId,
                                        UserName = p.UserName,
                                        ItemValue = p.ItemValue,
                                        EnableMinQty = p.EnableMinQty,
                                        MinQty = p.MinQty,
                                        EnableMaxQty = p.EnableMaxQty,
                                        MaxQty = p.MaxQty,
                                        EMSQty = p.EMSQty,
                                        EstRestockDate = p.EstRestockDate,
                                        Status = p.Status,
                                        ImageRelativePath = p.ImageRelativePath ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                                        Categories = p.Categories,
                                        CategoriesString = string.Join(",", p.Categories.Select(c => "[" + c.Id.ToString() + "]")),
                                        CategoriesDescString = string.Join(" || ", p.Categories.Select(c => c.Desc.TrimEnd()))
                                    });

                DataSourceResult result = productList.ToDataSourceResult(request, productvm => new ProductViewModel
                {
                    Id = productvm.Id,
                    EmsProductId = productvm.EmsProductId,
                    ProductCode = productvm.ProductCode,
                    PickPackCode = productvm.PickPackCode,
                    Upc = productvm.Upc,
                    ShortDesc = productvm.ShortDesc,
                    LongDesc = productvm.LongDesc,
                    Weight = productvm.Weight,
                    Length = productvm.Length,
                    Width = productvm.Width,
                    Height = productvm.Height,
                    Restricted = productvm.Restricted,
                    DefaultValue = productvm.DefaultValue,
                    SellPrice = productvm.SellPrice,
                    SellPriceCAD = productvm.SellPriceCAD,
                    LowLevel = productvm.LowLevel,
                    Uom = productvm.Uom,
                    CreatedBy = productvm.CreatedBy,
                    DateCreated = productvm.DateCreated,
                    UserName = productvm.UserName,
                    ItemValue = productvm.ItemValue,
                    EnableMinQty = productvm.EnableMinQty,
                    MinQty = productvm.MinQty,
                    EnableMaxQty = productvm.EnableMaxQty,
                    MaxQty = productvm.MaxQty,
                    EMSQty = productvm.EMSQty,
                    EstRestockDate = productvm.EstRestockDate,
                    OrderQty = productvm.OrderQty,
                    DisplayOrder = productvm.DisplayOrder,
                    Status = productvm.Status,
                    ImageRelativePath = productvm.ImageRelativePath ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                    Categories = productvm.Categories,
                    CategoriesString = productvm.CategoriesString,
                    CategoriesDescString = productvm.CategoriesDescString
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }


        // GET: Admin/StockDetail
        [TokenAuthorize]
        public ActionResult StockDetail(int? id, string statusMessage)
        {
            Session["CategoriesSelected"] = new List<CategoryViewModel>();
            ProductViewModel model = new ProductViewModel();
            try
            {
                var categorySelected = (from c in _sfDb.Categories
                                        join pc in _sfDb.ProductCategories on c.Id equals pc.CategoryId
                                        where pc.ProductId == id && c.StoreFrontId == _site.StoreFrontId
                                        group c by new { pc.ProductId, c.Name } into subc
                                        select new CategoryViewModel()
                                        {
                                            Name = subc.FirstOrDefault().Name,
                                            Desc = subc.FirstOrDefault().Desc
                                        }).ToList();

                ProductFile selectedProductFile = (from pf in _sfDb.ProductFiles
                                                   where pf.ProductId == id
                                                   select pf).FirstOrDefault();

                var productVm = (from p in _sfDb.Products
                                 join pi in _sfDb.ProductImages on p.Id equals pi.ProductId into subpi
                                 from pi in subpi.DefaultIfEmpty()
                                 where p.Id == id && p.StoreFrontId == _site.StoreFrontId && (pi.DisplayOrder ?? 1) == 1
                                 orderby p.PickPackCode
                                 select new ProductViewModel()
                                 {
                                     Id = p.Id,
                                     EmsProductId = p.EMSProductId,
                                     ProductCode = p.ProductCode,
                                     PickPackCode = p.PickPackCode,
                                     Upc = p.Upc,
                                     ShortDesc = p.ShortDesc,
                                     LongDesc = p.LongDesc,
                                     Weight = p.Weight,
                                     Length = p.Length,
                                     Width = p.Width,
                                     Height = p.Height,
                                     Restricted = p.Restricted == 1 ? true : false,
                                     DefaultValue = p.DefaultValue,
                                     SellPrice = p.SellPrice,
                                     SellPriceCAD = p.SellPriceCAD,
                                     LowLevel = p.LowLevel,
                                     Uom = p.Uom,
                                     CreatedBy = p.CreatedBy,
                                     DateCreated = p.DateCreated,
                                     UserId = p.UserId,
                                     UserName = p.UserName,
                                     ItemValue = p.ItemValue,
                                     EnableMinQty = p.EnableMinQty == 1 ? true : false,
                                     MinQty = p.MinQty,
                                     EnableMaxQty = p.EnableMaxQty == 1 ? true : false,
                                     MaxQty = p.MaxQty,
                                     EMSQty = p.EMSQty,
                                     EstRestockDate = p.EstRestockDate,
                                     Status = p.Status,
                                     IsFulfilledByVendor = p.IsFulfilledByVendor == 1,
                                     ImageRelativePath = pi.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Images/default.png" : pi.RelativePath,
                                 }).FirstOrDefault();

                if (productVm != null)
                {                    
                    productVm.Categories = categorySelected;                    
                    productVm.FileRelativePath = (selectedProductFile != null) ? selectedProductFile.RelativePath : "Content/" + _site.StoreFrontName + "/Files/default.png";
                    //productVm.ProductCategories = productCategories;
                    productVm.ProductCategories = (from pc in _sfDb.ProductCategories
                                                   join c in _sfDb.Categories on pc.CategoryId equals c.Id
                                                   where pc.ProductId == productVm.Id
                                                   select new ProductCategoriesVM()
                                                   {
                                                       Id = pc.Id,
                                                       ProductId = pc.ProductId,
                                                       CategoryId = pc.CategoryId,
                                                       Name = c.Name,
                                                       Desc = c.Desc,
                                                   }).OrderBy(c => c.Name).ToList();
                    productVm.ProductVariants = (from pv in _sfDb.ProductVariants
                                                 join v1 in _sfDb.VariantDetails on pv.Variant1 equals v1.Id
                                                 join v2 in _sfDb.VariantDetails on pv.Variant2 equals v2.Id
                                                 join v3 in _sfDb.VariantDetails on pv.Variant3 equals v3.Id
                                                 where pv.ProductId == productVm.Id
                                                 select new ProductVariantsVM  
                                                 {
                                                    Id = pv.Id,
                                                    ProductId = pv.ProductId,
                                                    Parent1 = v1.ParentId,
                                                    Parent2 = v2.ParentId,
                                                    Parent3 = v3.ParentId,
                                                    Variant1 = pv.Variant1 ?? 0,
                                                    Variant2 = pv.Variant2 ?? 0,
                                                    Variant3 = pv.Variant3 ?? 0,
                                                    Variant1Name = v1.VariantDetailName,
                                                    Variant2Name = v2.VariantDetailName,
                                                    Variant3Name = v3.VariantDetailName,
                                                 }).ToList();

                    productVm.VariantParent1Id = 1;
                    productVm.VariantParent2Id = 1;
                    productVm.VariantParent3Id = 1;
                    productVm.VariantDetail1Id = 1;
                    productVm.VariantDetail2Id = 1;
                    productVm.VariantDetail3Id = 1;
                    productVm.VariantDetail1Name = "";
                    productVm.VariantDetail2Name = "";
                    productVm.VariantDetail3Name = "";

                    foreach (ProductVariantsVM pv in productVm.ProductVariants)
                    {
                        productVm.VariantParent1Id = pv.Parent1;
                        productVm.VariantParent2Id = pv.Parent2;
                        productVm.VariantParent3Id = pv.Parent3;
                        productVm.VariantDetail1Id = pv.Variant1;
                        productVm.VariantDetail2Id = pv.Variant2;
                        productVm.VariantDetail3Id = pv.Variant3;
                        productVm.VariantDetail1Name = pv.Variant1Name;
                        productVm.VariantDetail2Name = pv.Variant2Name;
                        productVm.VariantDetail3Name = pv.Variant3Name;
                    }

                    productVm.ProductImages = (from pi in _sfDb.ProductImages
                                               where pi.ProductId == productVm.Id
                                               select new ProductImageVM()
                                               {
                                                   Id = pi.Id,
                                                   RelativePath = pi.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Images/default.png" : pi.RelativePath,
                                                   DateCreated = pi.DateCreated,
                                                   UserName = pi.UserName,
                                               }).ToList();
                    productVm.ProductFiles = (from pf in _sfDb.ProductFiles
                                              where pf.ProductId == productVm.Id
                                              select new ProductFileVM()
                                              {
                                                  Id = pf.Id,
                                                  RelativePath = pf.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Files/default.png" : pf.RelativePath,
                                                  DateCreated = pf.DateCreated,
                                                  UserName = pf.UserName,
                                              }).ToList();
                    foreach (ProductFileVM pf in productVm.ProductFiles)
                    {
                        pf.FileIcon = GrabIcon(Path.GetExtension(pf.RelativePath));
                    }
                }

                // Get the categories this product belongs to
                var selectedCategories = (from pc in _sfDb.ProductCategories
                                          join c in _sfDb.Categories on pc.CategoryId equals c.Id
                                          where pc.ProductId == id && c.StoreFrontId == _site.StoreFrontId
                                          select new CategoryViewModel()
                                          {
                                              Id = c.Id,
                                              Name = c.Name,
                                              Desc = c.Desc
                                          }).ToList();

                ViewBag.Categories = new SelectList(selectedCategories, "Name", "Name");

                var categoryUnselected1 = (from c in _sfDb.Categories
                                        where c.StoreFrontId == _site.StoreFrontId
                                        select new CategoryViewModel()
                                        {
                                            Id = c.Id,
                                            Name = c.Name,
                                            Desc = c.Desc
                                        }).ToList();
                var categoryUnselected = (from c in categoryUnselected1
                                          where !selectedCategories.Any(sc => sc.Id == c.Id)  
                                          select new CategoryViewModel()
                                         {
                                             Id = c.Id,
                                             Name = c.Name,
                                             Desc = c.Desc
                                         }).ToList();
                ViewBag.availableCategories = categoryUnselected;

                var variantParents = (from p in _sfDb.VariantParents
                                      where p.StoreFrontId == _site.StoreFrontId || p.StoreFrontId == 0
                                      select new VariantParentViewModel()
                                      {
                                          Id = p.Id,
                                          VariantParentName = p.VariantParentName
                                      }).ToList();
                ViewBag.VariantParents = variantParents;

                var variantDetails1 = (from d in _sfDb.VariantDetails
                                      where d.ParentId == productVm.VariantParent1Id
                                      select new VariantDetailViewModel()
                                      {
                                          Id = d.Id,
                                          ParentId = d.ParentId, 
                                          VariantDetailName = d.VariantDetailName
                                      }).ToList();
                ViewBag.VariantDetails1 = variantDetails1;

                var variantDetails2 = (from d in _sfDb.VariantDetails
                                       where d.ParentId == productVm.VariantParent2Id
                                       select new VariantDetailViewModel()
                                       {
                                           Id = d.Id,
                                           ParentId = d.ParentId,
                                           VariantDetailName = d.VariantDetailName
                                       }).ToList();
                ViewBag.VariantDetails2 = variantDetails2;

                var variantDetails3 = (from d in _sfDb.VariantDetails
                                       where d.ParentId == productVm.VariantParent3Id
                                       select new VariantDetailViewModel()
                                       {
                                           Id = d.Id,
                                           ParentId = d.ParentId,
                                           VariantDetailName = d.VariantDetailName
                                       }).ToList();
                ViewBag.VariantDetails3 = variantDetails3;

                // Set statuses
                ViewBag.Statuses = new SelectList(new List<SelectListItem>()
                                        { new SelectListItem()
                                              { Text = "Active", Value = "1" },
                                          new SelectListItem()
                                              { Text = "Inactive", Value = "0" }
                                        });

                ViewBag.StatusMessage = statusMessage;

                model = productVm;
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Inventory", "Stock"));
            }
            return View(model);
        }

        public string GrabIcon(string fileExtension)
        {
            switch (fileExtension)
            {
                case ".jpg":
                case ".img":
                case ".png":
                case ".gif":
                    return "jpg.png";
                case ".doc":
                case ".docx":
                    return "doc.png";
                case ".csv":
                case ".xls":
                case ".xlsx":
                    return "xls.png";
                case ".pdf":
                    return "pdf.png";
                case ".zip":
                case ".rar":
                    return "zip.png";
                default:
                    return "default.png";
            }
        }

        // POST: Inventory/StockDetail/5
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StockDetail(ProductViewModel model)
        {
            try
            {
                if (model.EnableMaxQty && model.MaxQty <= 0)
                {
                    ModelState.AddModelError("MaxQty", "Max Qty Required");
                }

                //var errors = ModelState
                //    .Where(x => x.Value.Errors.Count > 0)
                //    .Select(x => new { x.Key, x.Value.Errors })
                //    .ToArray();

                if (ModelState.IsValid)
                {
                    Product selectedProduct = _sfDb.Products.Find(model.Id);
                    ProductImage selectedProductImage = (from pi in _sfDb.ProductImages
                                                         where pi.ProductId == model.Id
                                                         select pi).FirstOrDefault();

                    selectedProduct.EMSProductId = model.EmsProductId;
                    selectedProduct.ProductCode = model.ProductCode;
                    selectedProduct.PickPackCode = model.PickPackCode;
                    selectedProduct.Upc = model.Upc;
                    selectedProduct.ShortDesc = model.ShortDesc;
                    selectedProduct.LongDesc = model.LongDesc;
                    selectedProduct.Weight = model.Weight;
                    selectedProduct.Length = model.Length;
                    selectedProduct.Width = model.Width;
                    selectedProduct.Height = model.Height;
                    selectedProduct.Restricted = model.Restricted ? 1 : 0;
                    selectedProduct.DefaultValue = model.DefaultValue;
                    selectedProduct.SellPrice = model.SellPrice;
                    selectedProduct.SellPriceCAD = model.SellPriceCAD;
                    selectedProduct.LowLevel = model.LowLevel;
                    selectedProduct.CreatedBy = model.CreatedBy;
                    selectedProduct.DateCreated = model.DateCreated;
                    selectedProduct.UserId = model.UserId;
                    selectedProduct.UserName = model.UserName;
                    selectedProduct.ItemValue = model.ItemValue;
                    selectedProduct.EnableMinQty = model.EnableMinQty ? 1 : 0;
                    selectedProduct.MinQty = model.MinQty;
                    selectedProduct.EnableMaxQty = model.EnableMaxQty ? 1 : 0;
                    selectedProduct.MaxQty = model.MaxQty;
                    selectedProduct.EMSQty = model.EMSQty;
                    selectedProduct.EstRestockDate = model.EstRestockDate;
                    selectedProduct.Status = model.Status;
                    selectedProduct.IsFulfilledByVendor = model.IsFulfilledByVendor ? 1 : 0;

                    //var savedImageRelativePath = (string)Session["ImageFilePath"];
                    //if (savedImageRelativePath != null && savedImageRelativePath.Length > 0)
                    //{
                    //    selectedProductImage.RelativePath = savedImageRelativePath;
                    //}

                    _sfDb.Entry(selectedProduct).State = EntityState.Modified;
                    _sfDb.SaveChanges();


                    try
                    {
                        // Save the product categories 
                        var CategoriesSelected = (List<CategoryViewModel>)Session["CategoriesSelected"];
                        if (CategoriesSelected != null && CategoriesSelected.Count > 0)
                        {
                            foreach (var c in CategoriesSelected)
                            {
                                ProductCategory selectedProductCategory = new ProductCategory();
                                selectedProductCategory.ProductId = model.Id;
                                selectedProductCategory.CategoryId = c.Id;
                                _sfDb.ProductCategories.Add(selectedProductCategory);
                                _sfDb.SaveChanges();
                            }
                        }
                    }
                    catch
                    {

                    }

                    return RedirectToAction("StockDetail", "Inventory", new { id = model.Id, statusMessage = "Saved Successfully" });
                }
                else
                {
                    //ModelState.AddModelError("UserName", "Username is not available");
                    ViewBag.StatusMessage = "Error Saving. Please check for error in the tabs.";
                    return RedirectToAction("StockDetail", "Inventory", new { id = model.Id, statusMessage = ViewBag.StatusMessage });
                }

                var categorySelected = (from c in _sfDb.Categories
                                        join pc in _sfDb.ProductCategories on c.Id equals pc.CategoryId
                                        where pc.ProductId == model.Id && c.StoreFrontId == _site.StoreFrontId
                                        group c by new { pc.ProductId, c.Name } into subc
                                        select new CategoryViewModel()
                                        {
                                            Name = subc.FirstOrDefault().Name,
                                            Desc = subc.FirstOrDefault().Desc
                                        }).ToList();

                ProductFile selectedProductFile = (from pf in _sfDb.ProductFiles
                                                   where pf.ProductId == model.Id
                                                   select pf).FirstOrDefault();

                model.Categories = categorySelected;
                model.FileRelativePath = (selectedProductFile != null) ? selectedProductFile.RelativePath : "Content/" + _site.StoreFrontName + "/Files/default.png";
                //model.ProductCategories = (from pc in _sfDb.ProductCategories
                //                       where pc.ProductId == model.Id
                //                       select new ProductCategoriesVM()
                //                       {
                //                           Id = pc.Id,
                //                           ProductId = pc.ProductId,
                //                           CategoryId = pc.CategoryId,
                //                           Name = "",
                //                           Desc = "",
                //                       }).ToList();
                model.ProductImages = (from pi in _sfDb.ProductImages
                                       where pi.ProductId == model.Id
                                       select new ProductImageVM()
                                       {
                                           Id = pi.Id,
                                           RelativePath = pi.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Images/default.png" : pi.RelativePath,
                                           DateCreated = pi.DateCreated,
                                           UserName = pi.UserName,
                                       }).ToList();
                model.ProductFiles = (from pf in _sfDb.ProductFiles
                                      where pf.ProductId == model.Id
                                      select new ProductFileVM()
                                      {
                                          Id = pf.Id,
                                          RelativePath = pf.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Files/default.png" : pf.RelativePath,
                                          DateCreated = pf.DateCreated,
                                          UserName = pf.UserName,
                                      }).ToList();
                foreach (ProductFileVM pf in model.ProductFiles)
                {
                    pf.FileIcon = GrabIcon(Path.GetExtension(pf.RelativePath));
                }

                // to avoid error in contentPath
                if (String.IsNullOrEmpty(model.ImageRelativePath)) model.ImageRelativePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                if (String.IsNullOrEmpty(model.ShortDesc)) model.ShortDesc = "";
                if (String.IsNullOrEmpty(model.LongDesc)) model.LongDesc = "";
                if (String.IsNullOrEmpty(model.ProductCode)) model.ProductCode = "";
                if (String.IsNullOrEmpty(model.PickPackCode)) model.PickPackCode = "";

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Inventory", "Stock"));
            }
        }

        // GET: Inventory/StockEdit (Not used currently)
        [TokenAuthorize]
        public ActionResult StockEdit(int? id)
        {
            ProductViewModel model = new ProductViewModel();
            try
            {
                var productVm = from p in _sfDb.Products
                                join pi in _sfDb.ProductImages on p.Id equals pi.ProductId into subpi
                                from pi in subpi.DefaultIfEmpty()
                                where p.Id == id && _sfIdList.Contains(p.StoreFrontId)
                                orderby p.PickPackCode
                                select new ProductViewModel()
                                {
                                    Id = p.Id,
                                    EmsProductId = p.EMSProductId,
                                    ProductCode = p.ProductCode,
                                    PickPackCode = p.PickPackCode,
                                    Upc = p.Upc,
                                    ShortDesc = p.ShortDesc,
                                    LongDesc = p.LongDesc,
                                    Weight = p.Weight,
                                    Length = p.Length,
                                    Width = p.Width,
                                    Height = p.Height,
                                    Restricted = p.Restricted == 1 ? true : false,
                                    DefaultValue = p.DefaultValue,
                                    SellPrice = p.SellPrice,
                                    SellPriceCAD = p.SellPriceCAD,
                                    LowLevel = p.LowLevel,
                                    Uom = p.Uom,
                                    CreatedBy = p.CreatedBy,
                                    DateCreated = p.DateCreated,
                                    UserId = p.UserId,
                                    UserName = p.UserName,
                                    ItemValue = p.ItemValue,
                                    MinQty = p.MinQty,
                                    MaxQty = p.MaxQty,
                                    EMSQty = p.EMSQty,
                                    EstRestockDate = p.EstRestockDate,
                                    Status = p.Status,
                                    ImageRelativePath = pi.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Images/default.png" : pi.RelativePath,
                                };

                model = productVm.FirstOrDefault();
            }

            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Inventory", "Stock"));
            }
            return View(model);
        }

        // POST: Inventory/StockEdit/5 (not used currently)
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StockEdit(ProductViewModel model)
        {
            if (ModelState.IsValid)
            {
                Product selectedProduct = _sfDb.Products.Find(model.Id);
                ProductImage selectedProductImage = (from pi in _sfDb.ProductImages
                                                     where pi.ProductId == model.Id
                                                     select pi).FirstOrDefault();

                selectedProduct.EMSProductId = model.EmsProductId;
                selectedProduct.ProductCode = model.ProductCode;
                selectedProduct.PickPackCode = model.PickPackCode;
                selectedProduct.Upc = model.Upc;
                selectedProduct.ShortDesc = model.ShortDesc;
                selectedProduct.LongDesc = model.LongDesc;
                selectedProduct.Weight = model.Weight;
                selectedProduct.Length = model.Length;
                selectedProduct.Width = model.Width;
                selectedProduct.Height = model.Height;
                selectedProduct.Restricted = model.Restricted ? 1 : 0;
                selectedProduct.DefaultValue = model.DefaultValue;
                selectedProduct.SellPrice = model.SellPrice;
                selectedProduct.SellPriceCAD = model.SellPriceCAD;
                selectedProduct.LowLevel = model.LowLevel;
                selectedProduct.Uom = model.Uom;
                selectedProduct.CreatedBy = model.CreatedBy;
                selectedProduct.DateCreated = model.DateCreated;
                selectedProduct.UserId = model.UserId;
                selectedProduct.UserName = model.UserName;
                selectedProduct.ItemValue = model.ItemValue;
                selectedProduct.MinQty = model.MinQty;
                selectedProduct.MaxQty = model.MaxQty;
                selectedProduct.EMSQty = model.EMSQty;
                selectedProduct.EstRestockDate = model.EstRestockDate;
                selectedProduct.Status = model.Status;

                var savedImageRelativePath = (string)Session["ImageFilePath"];
                if (savedImageRelativePath != null && savedImageRelativePath.Length > 0)
                {
                    selectedProductImage.RelativePath = savedImageRelativePath;
                }

                _sfDb.Entry(selectedProduct).State = EntityState.Modified;
                _sfDb.SaveChanges();

                return RedirectToAction("StockDetail", "Inventory", new { id = model.Id });
            }

            // to avoid error in contentPath
            if (String.IsNullOrEmpty(model.ImageRelativePath)) model.ImageRelativePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
            if (String.IsNullOrEmpty(model.ShortDesc)) model.ShortDesc = "";
            if (String.IsNullOrEmpty(model.LongDesc)) model.LongDesc = "";
            if (String.IsNullOrEmpty(model.ProductCode)) model.ProductCode = "";
            if (String.IsNullOrEmpty(model.PickPackCode)) model.PickPackCode = "";

            return View(model);
        }

        // GET: Admin/StockAdd
        [TokenAuthorize]
        public ActionResult StockAdd()
        {
            try
            {
                ProductViewModel model = new ProductViewModel();
                model.DateCreated = DateTime.Now;
                model.CreatedBy = _userName;
                model.UserId = _userSf.Id;
                model.UserName = _userSf.UserName;
                model.EnableMinQty = true;
                model.EnableMaxQty = true;
                model.Status = 1;
                model.ImageRelativePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                model.ProductImages = new List<ProductImageVM>();
                model.ProductFiles = new List<ProductFileVM>();
                model.MinQty = 1;
                model.MaxQty = 1;

                var categoryUnselected = (from c in _sfDb.Categories
                                           where c.StoreFrontId == _site.StoreFrontId
                                           select new CategoryViewModel()
                                           {
                                               Id = c.Id,
                                               Name = c.Name,
                                               Desc = c.Desc
                                           }).ToList();
                ViewBag.availableCategories = categoryUnselected;

                Session["ImageFilePath"] = "";
                Session["CategoriesSelected"] = new List<CategoryViewModel>();

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Inventory", "Stock"));
            }
        }

        // POST: Inventory/StockAdd/5
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StockAdd(ProductViewModel model)
        {
            try
            {
                //var CategoriesSelected = (List<CategoryViewModel>)Session["CategoriesSelected"];
                // Allow non-categorized product
                //if (CategoriesSelected == null)
                //{
                //    ModelState.AddModelError("", "Category not selected");
                //if (model.ImageRelativePath == null)
                //{
                //    model.ImageRelativePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                //}
                //    return View(model);
                //}

                if (model.EnableMaxQty && model.MaxQty <= 0)
                {
                    ModelState.AddModelError("MaxQty", "Max Qty Required");
                }

                var categoryUnselected = (from c in _sfDb.Categories
                                          where c.StoreFrontId == _site.StoreFrontId
                                          select new CategoryViewModel()
                                          {
                                              Id = c.Id,
                                              Name = c.Name,
                                              Desc = c.Desc
                                          }).ToList();
                ViewBag.availableCategories = categoryUnselected;

                if (ModelState.IsValid)
                {
                    int productId;
                    // find placeholder product/product image
                    Product selectedProduct = new Product()
                    {
                        ProductCode = "PlaceHolder",
                        StoreFrontId = _site.StoreFrontId,
                        EMSProductId = model.EmsProductId,
                        Weight = model.Weight,
                        Length = model.Length,
                        Width = model.Width,
                        Height = model.Height,
                        Restricted = model.Restricted ? 1 : 0,
                        DefaultValue = model.DefaultValue,
                        SellPrice = model.SellPrice,
                        SellPriceCAD = model.SellPriceCAD,
                        LowLevel = model.LowLevel,
                        CreatedBy = _userSf.UserName,
                        DateCreated = model.DateCreated,
                        UserId = model.UserId,
                        UserName = model.UserName,
                        ItemValue = model.ItemValue,
                        EnableMinQty = model.EnableMinQty ? 1 : 0,
                        MinQty = model.MinQty,
                        EnableMaxQty = model.EnableMaxQty ? 1 : 0,
                        MaxQty = model.MaxQty,
                        Status = 1,
                    };
                    List<Product> placeholderProducts = (from p in _sfDb.Products
                                                         where p.ProductCode == "PlaceHolder" && p.UserId == _userSf.Id && p.UserName == _userSf.UserName
                                                         select p).ToList();
                    if (placeholderProducts.Count() > 0)
                    {
                        foreach (Product p in placeholderProducts)
                            if (selectedProduct.Id == 0)
                                selectedProduct = p;
                            else
                                _sfDb.Products.Remove(p);
                    }
                    else
                    {
                        _sfDb.Products.Add(selectedProduct);
                    }
                    _sfDb.SaveChanges();

                    productId = selectedProduct.Id;

                    ProductImage selectedProductImage = new ProductImage();
                    //ProductCategory selectedProductCategory = new ProductCategory();

                    selectedProduct.StoreFrontId = _site.StoreFrontId;
                    selectedProduct.ProductCode = model.ProductCode;
                    selectedProduct.PickPackCode = model.PickPackCode;
                    selectedProduct.Upc = model.Upc;
                    selectedProduct.ShortDesc = model.ShortDesc;
                    selectedProduct.LongDesc = model.LongDesc;
                    selectedProduct.Weight = model.Weight;
                    selectedProduct.Length = model.Length;
                    selectedProduct.Width = model.Width;
                    selectedProduct.Height = model.Height;
                    selectedProduct.Restricted = model.Restricted ? 1 : 0;
                    selectedProduct.DefaultValue = model.DefaultValue;
                    selectedProduct.SellPrice = model.SellPrice;
                    selectedProduct.SellPriceCAD = model.SellPriceCAD;
                    selectedProduct.LowLevel = model.LowLevel;
                    selectedProduct.Uom = model.Uom;
                    selectedProduct.CreatedBy = model.CreatedBy;
                    selectedProduct.DateCreated = model.DateCreated;
                    selectedProduct.UserId = model.UserId;
                    selectedProduct.UserName = model.UserName;
                    selectedProduct.ItemValue = model.ItemValue;
                    selectedProduct.EnableMinQty = model.EnableMinQty ? 1 : 0;
                    selectedProduct.MinQty = model.MinQty;
                    selectedProduct.EnableMaxQty = model.EnableMaxQty ? 1 : 0;
                    selectedProduct.MaxQty = model.MaxQty;
                    selectedProduct.EMSQty = model.EMSQty;
                    selectedProduct.Status = 1;

                    _sfDb.Entry(selectedProduct).State = EntityState.Modified;

                    //_sfDb.Products.Add(selectedProduct);
                    _sfDb.SaveChanges();

                    //int newProductId = (from p in _sfDb.Products
                    //                    where p.ProductCode == model.ProductCode
                    //                    select p).FirstOrDefault().Id;

                    // Save the product categories 
                    //if (CategoriesSelected != null && CategoriesSelected.Count > 0)
                    //{
                    //    foreach (var c in CategoriesSelected)
                    //    {
                    //        selectedProductCategory.ProductId = productId;
                    //        selectedProductCategory.CategoryId = c.Id;
                    //        _sfDb.ProductCategories.Add(selectedProductCategory);
                    //    }
                    //    _sfDb.SaveChanges();
                    //}

                    try
                    {
                        var CategoriesSelected = (List<CategoryViewModel>)Session["CategoriesSelected"];
                        if (CategoriesSelected != null && CategoriesSelected.Count > 0)
                        {
                            foreach (var c in CategoriesSelected)
                            {
                                ProductCategory selectedProductCategory = new ProductCategory();
                                selectedProductCategory.ProductId = productId;
                                selectedProductCategory.CategoryId = c.Id;
                                _sfDb.ProductCategories.Add(selectedProductCategory);
                                _sfDb.SaveChanges();
                            }
                        }
                    }
                    catch
                    {

                    }


                    // Save the product images
                    //selectedProductImage.ProductId = newProductId;

                    //selectedProductImage.DateCreated = DateTime.Now;
                    //selectedProductImage.UserId = _userSf.Id;
                    //selectedProductImage.UserName = _userSf.UserName;
                    //selectedProductImage.DisplayOrder = 1;
                    //selectedProductImage.RelativePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                    //selectedProductImage.DefaultImagePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                    //selectedProductImage.FileName = "";
                    //var savedImageRelativePath = (string)Session["ImageFilePath"];
                    //if (savedImageRelativePath != null && savedImageRelativePath.Length > 0)
                    //{
                    //    selectedProductImage.RelativePath = savedImageRelativePath;
                    //}
                    //model.ImageRelativePath = selectedProductImage.RelativePath;
                    //_sfDb.ProductImages.Add(selectedProductImage);
                    // Product Images should already been saved, find it and change the productid
                    //selectedProductImage = (from pi in _sfDb.ProductImages
                    //                        where pi.ProductId == productId && pi.UserId == _userSf.Id && pi.UserName == _userSf.UserName
                    //                        select pi).FirstOrDefault();
                    //if (selectedProductImage != null)
                    //{
                    //    selectedProductImage.ProductId = productId;
                    //    _sfDb.Entry(selectedProductImage).State = EntityState.Modified;
                    //}

                    //_sfDb.SaveChanges();

                    ViewBag.Categories = new SelectList(_sfDb.Categories.Where(c => c.StoreFrontId == _site.StoreFrontId).ToList(), "Name", "Name");

                    return RedirectToAction("StockDetail", "Inventory", new { id = productId, statusMessage = "Saved Successfully" });
                }

                // to avoid error in contentPath
                if (String.IsNullOrEmpty(model.ImageRelativePath)) model.ImageRelativePath = "Content/" + _site.StoreFrontName + "/Images/default.png";
                if (String.IsNullOrEmpty(model.ShortDesc)) model.ShortDesc = "";
                if (String.IsNullOrEmpty(model.LongDesc)) model.LongDesc = "";
                if (String.IsNullOrEmpty(model.ProductCode)) model.ProductCode = "";
                if (String.IsNullOrEmpty(model.PickPackCode)) model.PickPackCode = "";

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Inventory", "Stock"));
            }
        }

        #endregion

        #region Vendor
        // GET: Manage/Vendor_Read (Not used currently)
        [TokenAuthorize]
        public ActionResult Vendor_Read([DataSourceRequest] DataSourceRequest request, int? selectedVendorId)
        {
            try
            {
                List<VendorViewModel> Vendors = new List<VendorViewModel>();
                Vendors = (from v in _sfDb.Vendors
                           join u in _sfDb.AspNetUsers on v.AspNetUserId equals u.Id
                           where v.StoreFrontId == _site.StoreFrontId
                           select new VendorViewModel()
                           {
                               Id = v.Id,
                               StoreFrontId = v.StoreFrontId,
                               AspNetUserId = v.AspNetUserId,
                               Alias = v.Alias,
                               Company = u.Company,
                               Address1 = u.Address1,
                               Address2 = u.Address2,
                               City = u.City,
                               State = u.State,
                               Zip = u.Zip,
                               Country = u.Country,
                               Phone = u.Phone,
                               UserName = u.UserName,
                               Email = u.Email,
                               Status = u.Status == 1,
                           }).ToList();

                DataSourceResult modelList = Vendors.ToDataSourceResult(request);

                return Json(modelList);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        // GET: Manage/Vendor_Create Not Currently Used
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public async System.Threading.Tasks.Task<ActionResult> Vendor_CreateAsync([DataSourceRequest] DataSourceRequest request, VendorViewModel vendor)
        {
            bool noerror = true;
            Vendor selectedVendor;

            // Existing alias?
            selectedVendor = _sfDb.Vendors.FirstOrDefault(v => v.Alias == vendor.Alias);
            if (selectedVendor != null)
            {
                ModelState.AddModelError("Alias", "Duplicate alias, please enter different alias");
            }

            if (vendor.Password == null || vendor.ConfirmPassword == null)
            {
                ModelState.AddModelError("", "Password / Confirmation invalid");
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var user = new ApplicationUser { UserName = vendor.UserName, Email = vendor.Email };
                    var result = await UserManager.CreateAsync(user, vendor.Password);
                    if (result.Succeeded)
                    {
                        // Add the profile data
                        AspNetUser selectedUser = _sfDb.AspNetUsers.Find(user.Id);
                        SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

                        UserSetting newUserSetting = new UserSetting()
                        {
                            AspNetUserId = user.Id,
                            StoreFrontId = _site.StoreFrontId,
                            BudgetIgnore = 0,
                            BudgetCurrentTotal = 0,
                            BudgetLastResetDate = new DateTime(1, 1, 1),
                            BudgetNextResetDate = new DateTime(1, 1, 1),
                            BudgetResetInterval = systemSetting.BudgetRefreshPeriodDefault,
                            BudgetLimit = systemSetting.BudgetLimitDefault,
                            AllowAdminAccess = 0,
                            AlertOrderReceived = 0,
                            AlertOrderShipped = 0,
                            AlertOnBudgetRefreshRequest = 0,
                        };
                        _sfDb.UserSettings.Add(newUserSetting);

                        List<UserStoreFront> selectedUserStoreFronts = new List<UserStoreFront>();
                        selectedUserStoreFronts.Add(new UserStoreFront() { AspNetUserId = user.Id, StoreFrontId = _site.StoreFrontId });

                        List<UserPermission> selectedUserPermissions = new List<UserPermission>();
                        selectedUserPermissions.Add(new UserPermission()
                        {
                            AspNetUserId = user.Id,
                            StoreFrontId = _site.StoreFrontId,
                            AdminUserModify = 0,
                            AdminSettingModify = 0,
                            InventoryItemModify = 0,
                            InventoryRestrictCategory = 0,
                            InventoryCategoryModify = 0,
                            OrderRestrictShipMethod = 0,
                            OrderCreate = 1,
                            OrderCancel = 0,
                            VendorModify = 0,
                        });

                        selectedUser.Email = vendor.Email;
                        selectedUser.UserName = vendor.UserName;
                        selectedUser.Company = vendor.Company;
                        selectedUser.CompanyAlias = vendor.CompanyAlias;
                        selectedUser.FirstName = "";
                        selectedUser.LastName = "";
                        selectedUser.Address1 = vendor.Address1;
                        selectedUser.Address2 = vendor.Address2;
                        selectedUser.City = vendor.City;
                        selectedUser.State = vendor.State;
                        selectedUser.Zip = vendor.Zip;
                        selectedUser.Country = vendor.Country;
                        selectedUser.Phone = vendor.Phone;
                        selectedUser.AccessRestricted = 0;
                        selectedUser.Status = 1;
                        selectedUser.FacilityId = 0;
                        selectedUser.OnHold = 0;
                        selectedUser.IsVendor = 1;

                        foreach (var up in selectedUserPermissions) { _sfDb.UserPermissions.Add(up); }
                        foreach (var usf in selectedUserStoreFronts) { _sfDb.UserStoreFronts.Add(usf); }

                        Vendor newVendor = new Vendor()
                        {
                            StoreFrontId = _site.StoreFrontId,
                            AspNetUserId = selectedUser.Id,
                            Alias = vendor.Alias,
                        };

                        _sfDb.Vendors.Add(newVendor);

                        try
                        {
                            if (noerror)
                            {
                                _sfDb.Entry(selectedUser).State = EntityState.Modified;
                                _sfDb.SaveChanges();
                                GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "Vendor Created", vendor.Alias);
                            }
                        }
                        catch (Exception ex)
                        {
                            return View("Error", new HandleErrorInfo(ex, "Inventory", "Vendors"));
                        }

                        return RedirectToAction("Vendors", "Inventory");
                    }
                    else
                    {
                        foreach (string error in result.Errors)
                        {
                            ModelState.AddModelError("", error);
                        }
                        return Json(new[] { vendor }.AsQueryable().ToDataSourceResult(request, ModelState), JsonRequestBehavior.AllowGet);
                    }
                }

            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { vendor }.AsQueryable().ToDataSourceResult(request, ModelState), JsonRequestBehavior.AllowGet);
        }

        // GET: Manage/Vendor_Update Not currently used
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Vendor_Update([DataSourceRequest] DataSourceRequest request, VendorViewModel vendor)
        {
            bool noerror = true;
            try
            {
                if (vendor != null && ModelState.IsValid)
                {
                    Vendor selectedVendor = _sfDb.Vendors.FirstOrDefault(v => v.Id == vendor.Id);
                    if (selectedVendor != null)
                    {
                    };

                    _sfDb.Entry(selectedVendor).State = EntityState.Modified;
                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "Vendor Modified", selectedVendor.Alias);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { vendor }.ToDataSourceResult(request, ModelState));
        }

        // GET: Manage/Vendors_Destroy
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Vendor_Destroy([DataSourceRequest] DataSourceRequest request, VendorViewModel vendor)
        {
            bool noerror = true;
            try
            {
                if (vendor != null && ModelState.IsValid)
                {
                    Vendor selectedVendor = _sfDb.Vendors.FirstOrDefault(v => v.Id == vendor.Id);
                    if (selectedVendor != null)
                    {
                        _sfDb.Vendors.Remove(selectedVendor);
                    };

                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "Vendor Removed", selectedVendor.Alias);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { vendor }.ToDataSourceResult(request, ModelState));
        }

        // GET: Manage/GetCountries
        [TokenAuthorize]
        public ActionResult GetVendorUsers()
        {
            List<SelectListItem> users = new List<SelectListItem>();
            List<AspNetUser> storeFrontVendors = (from u in _sfDb.AspNetUsers
                                                  join usf in _sfDb.UserStoreFronts on u.Id equals usf.AspNetUserId
                                                  where usf.StoreFrontId == _site.StoreFrontId && u.IsVendor == 1
                                                  select u).ToList();
            users.AddRange(storeFrontVendors.Select(u => new SelectListItem() { Text = u.Email, Value = u.Id }).OrderBy(c => c.Text).ToList());

            return Json(users, JsonRequestBehavior.AllowGet);
        }

        #endregion

        [TokenAuthorize]
        public ActionResult GetUoms()
        {
            try
            {
                var uoms = (from uom in _sfDb.MasterUOMs
                            orderby uom.Acronym
                            select new
                            {
                                Id = uom.Id,
                                Acronym = uom.Acronym
                            }).ToList();

                return Json(uoms, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        #region StockDetail
        [TokenAuthorize]
        public ActionResult UI_ToggleStatus(int id)
        {
            try
            {
                int currentStatus = -1;

                var queryStatus = from p in _sfDb.Products
                                  where p.Id == id
                                  select p;

                if (queryStatus.Count() > 0)
                {
                    currentStatus = queryStatus.FirstOrDefault().Status;

                    if (currentStatus == 1)
                        currentStatus = 0;
                    else
                        currentStatus = 1;

                    Product selectedProduct = queryStatus.FirstOrDefault();
                    selectedProduct.Status = (short)currentStatus;
                    _sfDb.Entry(selectedProduct).State = EntityState.Modified;
                    _sfDb.SaveChanges();
                }

                return Json(new { result = "Success", status = currentStatus }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", status = "Error", message = ex.Message });
            }
        }

        #region Category Tab
        [TokenAuthorize]
        public ActionResult ToolbarTemplate_Categories(Product model)
        {
            try
            {
                //var categories = (from pc in _sfDb.ProductCategories
                //                  join c in _sfDb.Categories on pc.CategoryId equals c.Id
                //                  where pc.ProductId == model.Id
                //                  orderby c.Name
                //                  select new CategoryViewModel
                //                  {
                //                      Id = c.Id,
                //                      Name = c.Name
                //                  }).ToList();

                //if (_userRoleList.Contains("Admin") || _userRoleList.Contains("SuperAdmin"))
                //{
                // load everything you're in this menu so you're an admin
                var categories = _sfDb.Categories.Where(c => c.StoreFrontId == _site.StoreFrontId)
                            .Select(c => new CategoryViewModel
                            {
                                Id = c.Id,
                                Name = c.Name
                            })
                            .OrderBy(e => e.Name).ToList();
                //}

                if (model != null)
                {
                    if (model.Id != 0)
                    {
                        var selectedCategories = (from pc in _sfDb.ProductCategories
                                                  join c in _sfDb.Categories on pc.CategoryId equals c.Id
                                                  where pc.ProductId == model.Id
                                                  select c.Name).ToList();

                        // Remove selected categories
                        foreach (var sc in selectedCategories)
                        {
                            categories.Remove(categories.Where(c => c.Name == sc).FirstOrDefault());
                        }
                    }
                }


                return Json(categories, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult ToolbarTemplate_Categories_SetSelected(Product model)
        {
            try
            {
                var selectedCategories = (from pc in _sfDb.ProductCategories
                                          join c in _sfDb.Categories on pc.CategoryId equals c.Id
                                          where pc.ProductId == model.Id
                                          select new CategoryViewModel()
                                          {
                                              Id = c.Id,
                                              Name = c.Name
                                          }).ToList();

                return Json(selectedCategories, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult reload_VariantDetails(int ParentId)
        {      
            try
            {
                //var categories = new List<CategoryViewModel>();
                var variantDetails = (from d in _sfDb.VariantDetails
                                      where d.ParentId == ParentId
                                      select new VariantDetailViewModel()
                                      {
                                          Id = d.Id,
                                          ParentId = d.ParentId,
                                          VariantDetailName = d.VariantDetailName
                                      }).ToList();
                return Json(variantDetails);
                //return Json(new { result = "ParentId is 0" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Categories_SaveSelected(List<int> categoryIdList)
        {
            Session["CategoriesSelected"] = "";
            try
            {
                var categories = new List<CategoryViewModel>();
                foreach (var cid in categoryIdList)
                {
                    var selectedCategoryList = (from c in _sfDb.Categories
                                                   where c.Id == cid
                                                   select c).FirstOrDefault();
                    
                    categories.Add(new CategoryViewModel
                    {
                        Id = selectedCategoryList.Id,
                        Name = selectedCategoryList.Name,
                        Desc = selectedCategoryList.Desc
                    });
                }
                Session["CategoriesSelected"] = categories;
                return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);                
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }


        [TokenAuthorize]
        public ActionResult ToolbarTemplate_Categories_SaveSelected(List<CategoryViewModel> categories, Product product)
        {
            try
            {
                if (product.Id != 0)
                {
                    var prevProductCategoryList = (from c in _sfDb.ProductCategories
                                                   where c.ProductId == product.Id
                                                   select c).ToList();

                    var curSelectedCategoryList = categories;

                    // removed category
                    foreach (var c in prevProductCategoryList)
                    {
                        bool shouldDelete = false;
                        if (categories == null) shouldDelete = true;
                        else
                        {
                            var query = from input in categories
                                        where input.Id == c.CategoryId
                                        select input;
                            if (query.Count() == 0) shouldDelete = true;
                        }

                        if (shouldDelete)
                        {
                            var deleteMe = (from pc in _sfDb.ProductCategories
                                            where pc.Id == c.Id
                                            select pc).FirstOrDefault();
                            _sfDb.ProductCategories.Remove(deleteMe);
                        }
                    }

                    // added category
                    if (curSelectedCategoryList != null)
                    {
                        foreach (var c in curSelectedCategoryList)
                        {
                            var query = from prevPc in prevProductCategoryList
                                        where prevPc.CategoryId == c.Id
                                        select prevPc;
                            if (query.Count() == 0)
                            {
                                var addMe = new ProductCategory()
                                {
                                    ProductId = product.Id,
                                    CategoryId = c.Id
                                };
                                _sfDb.ProductCategories.Add(addMe);
                            }
                        }
                    }

                    _sfDb.SaveChanges();

                    return Json(new { result = "Success" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // Save the categories anyways in the Session
                    Session["CategoriesSelected"] = categories;

                    return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }
        #endregion Category Tab

        #region Vendor Tab
        // GET: Manage/GetVendors
        [TokenAuthorize]
        public ActionResult GetVendors(Product model)
        {
            List<VendorViewModel> vendors = new List<VendorViewModel>();
            vendors.AddRange(_sfDb.Vendors.Where(v => v.StoreFrontId == _site.StoreFrontId).Select(v => new VendorViewModel() { Alias = v.Alias, Id = v.Id }).OrderBy(v => v.Alias).ToList());

            if (model != null && model.Id != 0)
            {
                List<string> selectedStockVendors = (from vp in _sfDb.VendorProducts
                                                     join v in _sfDb.Vendors on vp.VendorId equals v.Id
                                                     where vp.ProductId == model.Id
                                                     select v.Alias).ToList();

                // Remove selected vendors
                foreach (var sc in selectedStockVendors)
                {
                    vendors.Remove(vendors.Where(c => c.Alias == sc).FirstOrDefault());
                }
            }

            return Json(vendors, JsonRequestBehavior.AllowGet);
        }

        [TokenAuthorize]
        public ActionResult StockVendors_SetSelected(Product model)
        {
            try
            {
                var selectedStockVendors = (from vp in _sfDb.VendorProducts
                                            join v in _sfDb.Vendors on vp.VendorId equals v.Id
                                            where vp.ProductId == model.Id
                                            select new VendorViewModel()
                                            {
                                                Id = v.Id,
                                                Alias = v.Alias
                                            }).ToList();

                return Json(selectedStockVendors, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult StockVendors_SaveSelected(bool isFulfilledByVendor, List<VendorViewModel> selectedVendors, Product product)
        {
            try
            {
                if (product.Id != 0)
                {
                    var prevVendorProductList = (from c in _sfDb.VendorProducts
                                                 where c.ProductId == product.Id
                                                 select c).ToList();

                    var curSelectedVendorProductList = selectedVendors;

                    // removed vendors
                    foreach (var c in prevVendorProductList)
                    {
                        bool shouldDelete = false;
                        if (selectedVendors == null) shouldDelete = true;
                        else
                        {
                            var query = from sv in selectedVendors
                                        where sv.Id == c.VendorId
                                        select sv;
                            if (query.Count() == 0) shouldDelete = true;
                        }

                        if (shouldDelete)
                        {
                            var deleteMe = (from vp in _sfDb.VendorProducts
                                            where vp.Id == c.Id
                                            select vp).FirstOrDefault();
                            _sfDb.VendorProducts.Remove(deleteMe);
                        }
                    }

                    // added vendors
                    if (curSelectedVendorProductList != null)
                    {
                        foreach (var vp in curSelectedVendorProductList)
                        {
                            var query = from prevVp in prevVendorProductList
                                        where prevVp.VendorId == vp.Id
                                        select prevVp;
                            if (query.Count() == 0)
                            {
                                var addMe = new VendorProduct()
                                {
                                    ProductId = product.Id,
                                    VendorId = vp.Id
                                };
                                _sfDb.VendorProducts.Add(addMe);
                            }
                        }
                    }

                    Product selectedProduct = _sfDb.Products.Where(p => p.Id == product.Id).FirstOrDefault();
                    selectedProduct.IsFulfilledByVendor = isFulfilledByVendor ? 1 : 0;

                    _sfDb.Entry(selectedProduct).State = EntityState.Modified;
                    _sfDb.SaveChanges();

                    return Json(new { result = "Success" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }
        #endregion Vendor Tab


        #endregion StockDetail

        #region Cart_CheckoutProcess
        // GET: Manage/GetVendorsForProduct
        [TokenAuthorize]
        public ActionResult GetVendorsForProduct(Product model)
        {
            List<VendorViewModel> vendorsForProduct = new List<VendorViewModel>();

            if (model != null && model.Id != 0)
            {
                vendorsForProduct = (from vp in _sfDb.VendorProducts
                                     join v in _sfDb.Vendors on vp.VendorId equals v.Id
                                     where vp.ProductId == model.Id
                                     select new VendorViewModel()
                                     {
                                         Alias = v.Alias,
                                         Id = v.Id,
                                     })
                                    .OrderBy(v => v.Alias)
                                    .ToList();
            }

            return Json(vendorsForProduct, JsonRequestBehavior.AllowGet);
        }
        #endregion


        [TokenAuthorize]
        public ActionResult Category()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Inventory", "Stock"));
            }
        }

        [TokenAuthorize]
        public ActionResult Categories_Read([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                IQueryable<Category> categories = _sfDb.Categories.Where(c => c.StoreFrontId == _site.StoreFrontId).OrderBy(c => c.Name);
                DataSourceResult result = categories.ToDataSourceResult(request, category => new CategoryViewModel
                {
                    Id = category.Id,
                    Name = category.Name,
                    Desc = category.Desc
                });

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Categories_Create([DataSourceRequest] DataSourceRequest request, CategoryViewModel category)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var entity = new Category
                    {
                        StoreFrontId = _site.StoreFrontId,
                        Name = category.Name,
                        Desc = category.Desc,
                        DateCreated = DateTime.Now,
                        UserId = _userSf.Id,
                        UserName = _userSf.UserName
                    };

                    _sfDb.Categories.Add(entity);
                    _sfDb.SaveChanges();
                    category.Id = entity.Id;
                }

                return Json(new[] { category }.ToDataSourceResult(request, ModelState));
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Categories_Update([DataSourceRequest] DataSourceRequest request, CategoryViewModel category)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var selectedCategory = (from c in _sfDb.Categories
                                            where c.Id == category.Id
                                            select c).FirstOrDefault();

                    selectedCategory.Name = category.Name;
                    selectedCategory.Desc = category.Desc;

                    _sfDb.Entry(selectedCategory).State = EntityState.Modified;
                    _sfDb.SaveChanges();
                }

                return Json(new[] { category }.ToDataSourceResult(request, ModelState));
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Categories_Destroy([DataSourceRequest] DataSourceRequest request, CategoryViewModel category)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // remove this category from users associated
                    List<UserCategory> usersWithCategory = (from uc in _sfDb.UserCategories where uc.CategoryId == category.Id select uc).ToList();
                    if (usersWithCategory.Count() > 0)
                    {
                        foreach (var uc in usersWithCategory)
                        {
                            _sfDb.UserCategories.Remove(uc);
                        }
                    }

                    // remove this category from products associated
                    List<ProductCategory> productsWithCategory = (from pc in _sfDb.ProductCategories where pc.CategoryId == category.Id select pc).ToList();
                    if (productsWithCategory.Count() > 0)
                    {
                        foreach (var pc in productsWithCategory)
                        {
                            _sfDb.ProductCategories.Remove(pc);
                        }
                    }

                    // remove the category
                    var selectedCategory = (from c in _sfDb.Categories
                                            where c.Id == category.Id
                                            select c).FirstOrDefault();

                    _sfDb.Categories.Remove(selectedCategory);
                    _sfDb.SaveChanges();
                }

                return Json(new[] { category }.ToDataSourceResult(request, ModelState));
            }
            catch (Exception ex)
            {
                return Json(new DataSourceResult { Errors = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Vendor()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Inventory", "Stock"));
            }
        }

        protected override void Dispose(bool disposing)
        {
            _sfDb.Dispose();
            base.Dispose(disposing);
        }
    }
}
