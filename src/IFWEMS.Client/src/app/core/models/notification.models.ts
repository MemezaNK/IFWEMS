export interface NotificationDto {
  id: string;
  subject: string;
  body: string;
  isRead: boolean;
  sentAtUtc: string | null;
}
