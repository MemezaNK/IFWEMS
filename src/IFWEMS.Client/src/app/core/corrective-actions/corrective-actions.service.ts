import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CorrectiveActionDto,
  CreateCorrectiveActionRequest,
  VerifyCorrectiveActionRequest
} from '../models/corrective-action.models';

@Injectable({ providedIn: 'root' })
export class CorrectiveActionsService {
  private readonly baseUrl = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  getForCase(caseId: string): Observable<CorrectiveActionDto[]> {
    return this.http.get<CorrectiveActionDto[]>(`${this.baseUrl}/cases/${caseId}/corrective-actions`);
  }

  create(caseId: string, request: CreateCorrectiveActionRequest): Observable<CorrectiveActionDto> {
    return this.http.post<CorrectiveActionDto>(`${this.baseUrl}/cases/${caseId}/corrective-actions`, request);
  }

  verify(id: string, request: VerifyCorrectiveActionRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/corrective-actions/${id}/verify`, request);
  }
}
