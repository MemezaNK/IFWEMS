import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EmergencyOverrideRequest, TransactionCheckRequest, TransactionCheckResult } from '../models/compliance.models';

@Injectable({ providedIn: 'root' })
export class ComplianceService {
  private readonly baseUrl = `${environment.apiBaseUrl}/compliance`;

  constructor(private readonly http: HttpClient) {}

  check(request: TransactionCheckRequest): Observable<TransactionCheckResult> {
    return this.http.post<TransactionCheckResult>(`${this.baseUrl}/check`, request);
  }

  override(request: EmergencyOverrideRequest): Observable<{ transactionId: string }> {
    return this.http.post<{ transactionId: string }>(`${this.baseUrl}/override`, request);
  }
}
