namespace Electronic_Election_Management_System.Models
{
    public enum ElectionInvitationMethod
    {
        Manual,
        Email
    }

    /// <summary>
    /// Grants access to a closed election. Email is always stored so invitations sent
    /// before account registration become effective as soon as that email registers.
    /// </summary>
    public class ElectionInvitation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }

        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public string Email { get; set; } = string.Empty;
        public ElectionInvitationMethod Method { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
