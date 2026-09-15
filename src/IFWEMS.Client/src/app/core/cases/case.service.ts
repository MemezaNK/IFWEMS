import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CaseDto, ChangeCaseStatusRequest, CreateCaseRequest } from '../models/case.models';

@Injectable({ providedIn: 'root' })
export class CaseService {
  private readonly baseUrl = `${environment.apiBaseUrl}/cases`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<CaseDto[]> {
    return this.http.get<CaseDto[]>(this.baseUrl);
  }

  getById(id: string): Observable<CaseDto> {
    return this.http.get<CaseDto>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateCaseRequest): Observable<CaseDto> {
    return this.http.post<CaseDto>(this.baseUrl, request);
  }

  changeStatus(id: string, request: ChangeCaseStatusRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/status`, request);
  }
}
