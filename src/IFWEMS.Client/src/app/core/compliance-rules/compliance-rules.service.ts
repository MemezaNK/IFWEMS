import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ComplianceRuleDto, CreateComplianceRuleRequest } from '../models/compliance-rule.models';

@Injectable({ providedIn: 'root' })
export class ComplianceRulesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/compliance/rules`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ComplianceRuleDto[]> {
    return this.http.get<ComplianceRuleDto[]>(this.baseUrl);
  }

  create(request: CreateComplianceRuleRequest): Observable<ComplianceRuleDto> {
    return this.http.post<ComplianceRuleDto>(this.baseUrl, request);
  }

  approve(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/approve`, {});
  }
}
