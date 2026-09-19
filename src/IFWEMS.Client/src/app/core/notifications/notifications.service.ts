import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NotificationDto } from '../models/notification.models';

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/notifications`;

  constructor(private readonly http: HttpClient) {}

  getMine(): Observable<NotificationDto[]> {
    return this.http.get<NotificationDto[]>(this.baseUrl);
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/mark-read`, {});
  }
}
