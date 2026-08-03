export interface Notification {
    id: string;
    userId: string;
    message: string;
    createdAt: string;
    isRead: boolean;
    type?: string;
    referenceId?: string;
}
