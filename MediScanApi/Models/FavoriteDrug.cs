namespace MediScanApi.Models
{
    public class FavoriteDrug
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string Warnings { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}