using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.Models;

public class PaymentTransaction
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    public PaymentMethod PaymentMethod { get; set; }

    [MaxLength(450)]
    public string? RequestedByUserId { get; set; }

    public decimal AmountUSD { get; set; }

    public long AmountVND { get; set; }

    public decimal ExchangeRate { get; set; }

    public PaymentTransactionStatus Status { get; set; }
        = PaymentTransactionStatus.Pending;

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}