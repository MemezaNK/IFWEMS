import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateSystemSettingRequest,
  SystemSettingDto,
  UpdateSystemSettingRequest
} from '../models/system-config.models';

@Injectable({ providedIn: 'root' })
export class SystemConfigService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/system-config`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<SystemSettingDto[]> {
    return this.http.get<SystemSettingDto[]>(this.baseUrl);
  }

  create(request: CreateSystemSettingRequest): Observable<SystemSettingDto> {
    return this.http.post<SystemSettingDto>(this.baseUrl, request);
  }

  update(id: string, request: UpdateSystemSettingRequest): Observable<SystemSettingDto> {
    return this.http.put<SystemSettingDto>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
