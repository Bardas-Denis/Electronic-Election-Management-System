using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories;

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task AddAsync(Notification notification);
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllAsReadAsync(Guid userId);
}
