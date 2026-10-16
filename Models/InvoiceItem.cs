using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.Models;

public class InvoiceItem
{
    public int Id { get; set; }

    [Range(1, int.MaxValue)]
    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    [EnumDataType(typeof(InvoiceItemType))]
    public InvoiceItemType ItemType { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "100000000")]
    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }
}