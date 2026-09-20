export interface NotificationTemplateDto {
  id: string;
  code: string;
  subject: string;
  bodyHtml: string;
  isActive: boolean;
  modifiedAtUtc: string | null;
}

export interface CreateNotificationTemplateRequest {
  code: string;
  subject: string;
  bodyHtml: string;
  isActive: boolean;
}

export interface UpdateNotificationTemplateRequest {
  subject: string;
  bodyHtml: string;
  isActive: boolean;
}
