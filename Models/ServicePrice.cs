namespace ihouse.Models
{
    public class ServicePrice
    {
        public int Id { get; set; }

        public ServiceType ServiceType { get; set; }

        public decimal Price { get; set; }

        public DateOnly EffectiveFrom { get; set; }

        public DateOnly? EffectiveTo { get; set; }

        public bool IsActive { get; set; }
    }
}