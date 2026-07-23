namespace Electronic_Election_Management_System.Models
{
    public class ElectionQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public ICollection<Option> Options { get; set; } = new List<Option>();
    }
}
