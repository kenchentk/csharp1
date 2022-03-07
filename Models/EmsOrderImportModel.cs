using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Models
{
    public class EmsOrderImportModel
    {
        public int OrderId { get; set; }
        public int OrderNumber { get; set; }
        public int UserId { get; set; }
        public int CustomerId { get; set; }
        public int ShipId { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public Nullable<System.DateTime> ShipDate { get; set; }
        public string Comments { get; set; }
        public string OrderStatus { get; set; }
        public string AltOrderNumber { get; set; }
        public string Order_Urgency { get; set; }
        public string PONumber { get; set; }
        public string ShipAccount { get; set; }
        public int ShipBillType { get; set; }
        public double Insure { get; set; }
        public int OnHold { get; set; }
        public int ShipAhead { get; set; }
        public int Signature { get; set; }
        public string ShipNotes { get; set; }
        public int EditLock { get; set; }
        public Nullable<System.DateTime> LockTime { get; set; }
        public int OrderType { get; set; }
        public string ErrorNotes { get; set; }
        public Nullable<System.DateTime> FutureReleaseDate { get; set; }
        public int Exported { get; set; }
        public double DeclaredValue { get; set; }
        public int OrderBatchId { get; set; }
        public int OrderImportBatchId { get; set; }
        public int StoreFrontOrder { get; set; }
        public int DefaultNotify { get; set; }
        public string ImportReference1 { get; set; }
        public string ImportReference2 { get; set; }
        public int ShipperId { get; set; }
        public string ShipSubject { get; set; }
        public string ShipMessage { get; set; }
        public int IntBillType { get; set; }
        public int Disable_PackingSlips { get; set; }
        public int Disable_Labels { get; set; }
        public int Specialty { get; set; }
        public int ProcessBatch { get; set; }
        public Nullable<System.DateTime> ShipByDate { get; set; }
        public int Billed { get; set; }
        public int Billable { get; set; }
        public string Custom_Ship_Message { get; set; }
        public int Release { get; set; }
    }
}