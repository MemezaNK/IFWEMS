import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateSlaPolicyRequest, OverdueCaseDto, SlaPolicyDto } from '../models/sla.models';

@Injectable({ providedIn: 'root' })
export class SlaService {
  private readonly baseUrl = `${environment.apiBaseUrl}/sla`;

  constructor(private readonly http: HttpClient) {}

  getPolicies(): Observable<SlaPolicyDto[]> {
    return this.http.get<SlaPolicyDto[]>(`${this.baseUrl}/policies`);
  }

  createPolicy(request: CreateSlaPolicyRequest): Observable<SlaPolicyDto> {
    return this.http.post<SlaPolicyDto>(`${this.baseUrl}/policies`, request);
  }

  getOverdueCases(): Observable<OverdueCaseDto[]> {
    return this.http.get<OverdueCaseDto[]>(`${this.baseUrl}/overdue-cases`);
  }
}
