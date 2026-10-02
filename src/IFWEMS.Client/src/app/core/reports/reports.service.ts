import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/reports`;

  constructor(private readonly http: HttpClient) {}

  downloadCasesRegister(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/cases.csv`, { responseType: 'blob' });
  }

  downloadRecoveriesRegister(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/recoveries.csv`, { responseType: 'blob' });
  }

  downloadContractsRegister(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/contracts.csv`, { responseType: 'blob' });
  }

  /** Current user's own activity report (what they've done / what they still need to do). */
  downloadMyActivityReport(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/user-activity.pdf`, { responseType: 'blob' });
  }

  /** A specific user's activity report. SystemAdministrator/ComplianceOfficer only. */
  downloadUserActivityReport(userId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/user-activity.pdf`, { responseType: 'blob', params: { userId } });
  }

  /** Organisation-wide activity summary across all active users. SystemAdministrator/ComplianceOfficer only. */
  downloadAllUsersActivityReport(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/user-activity.pdf`, { responseType: 'blob', params: { all: true } });
  }
}
