import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateNotificationTemplateRequest,
  NotificationTemplateDto,
  UpdateNotificationTemplateRequest
} from '../models/notification-template.models';

@Injectable({ providedIn: 'root' })
export class NotificationTemplatesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/notification-templates`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<NotificationTemplateDto[]> {
    return this.http.get<NotificationTemplateDto[]>(this.baseUrl);
  }

  create(request: CreateNotificationTemplateRequest): Observable<NotificationTemplateDto> {
    return this.http.post<NotificationTemplateDto>(this.baseUrl, request);
  }

  update(id: string, request: UpdateNotificationTemplateRequest): Observable<NotificationTemplateDto> {
    return this.http.put<NotificationTemplateDto>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
