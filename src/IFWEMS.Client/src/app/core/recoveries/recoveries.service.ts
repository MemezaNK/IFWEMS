import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApproveWriteOffRequest, CaptureRecoveryRequest, RecoveryDto } from '../models/recovery.models';

@Injectable({ providedIn: 'root' })
export class RecoveriesService {
  private readonly baseUrl = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  getForCase(caseId: string): Observable<RecoveryDto[]> {
    return this.http.get<RecoveryDto[]>(`${this.baseUrl}/cases/${caseId}/recoveries`);
  }

  capture(caseId: string, request: CaptureRecoveryRequest): Observable<RecoveryDto> {
    return this.http.post<RecoveryDto>(`${this.baseUrl}/cases/${caseId}/recoveries`, request);
  }

  approveWriteOff(id: string, request: ApproveWriteOffRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/recoveries/${id}/write-off`, request);
  }
}
