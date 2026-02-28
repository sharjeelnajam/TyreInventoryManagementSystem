namespace Domain
{
    public class CustomerSalerPrice : BaseEntity
    {
        public Guid CustomerId { get; set; }

        public Guid ThreadId { get; set; }   // ListManagement (Thread)
        public Guid UnitId { get; set; }   // ListManagement (Unit)

        public decimal Price { get; set; }

        // Navigation (for customers with type Wholesaler)
        public Customer Customer { get; set; }
    }
}
