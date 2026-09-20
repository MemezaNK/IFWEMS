import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditLogPageDto, AuditLogQuery } from '../models/audit.models';

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly baseUrl = `${environment.apiBaseUrl}/audit`;

  constructor(private readonly http: HttpClient) {}

  query(query: AuditLogQuery): Observable<AuditLogPageDto> {
    let params = new HttpParams();
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    });
    return this.http.get<AuditLogPageDto>(this.baseUrl, { params });
  }

  getEntityNames(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/entity-names`);
  }
}
