namespace MediScanApi.Models
{
    public class DrugNote
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public string NoteText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}