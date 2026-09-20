export interface SystemSettingDto {
  id: string;
  key: string;
  value: string;
  category: string;
  description: string | null;
  modifiedAtUtc: string | null;
}

export interface CreateSystemSettingRequest {
  key: string;
  value: string;
  category: string;
  description: string | null;
}

export interface UpdateSystemSettingRequest {
  value: string;
  description: string | null;
}
