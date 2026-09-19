import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApproveInvestigationRequest,
  CreateInvestigationRequest,
  InvestigationDto
} from '../models/investigation.models';

@Injectable({ providedIn: 'root' })
export class InvestigationsService {
  private readonly baseUrl = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  getForCase(caseId: string): Observable<InvestigationDto[]> {
    return this.http.get<InvestigationDto[]>(`${this.baseUrl}/cases/${caseId}/investigations`);
  }

  create(caseId: string, request: CreateInvestigationRequest): Observable<InvestigationDto> {
    return this.http.post<InvestigationDto>(`${this.baseUrl}/cases/${caseId}/investigations`, request);
  }

  approve(id: string, request: ApproveInvestigationRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/investigations/${id}/approve`, request);
  }
}
