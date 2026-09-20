export interface AuditLogDto {
  id: number;
  userId: string | null;
  username: string | null;
  action: string;
  entityName: string;
  entityId: string | null;
  beforeValuesJson: string | null;
  afterValuesJson: string | null;
  timestampUtc: string;
  ipAddress: string | null;
}

export interface AuditLogPageDto {
  items: AuditLogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogQuery {
  entityName?: string;
  username?: string;
  action?: string;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}
