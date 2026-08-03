using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Electronic_Election_Management_System.Models;

public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = null!;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;

    public string? Type { get; set; }

    public Guid? ReferenceId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
