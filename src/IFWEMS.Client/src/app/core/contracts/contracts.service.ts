import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ContractDto } from '../models/contract.models';

export interface CreateContractRequest {
  contractNumber: string;
  title: string;
  supplierId: string;
  originalValue: number;
  startDateUtc: string;
  expiryDateUtc: string;
}

@Injectable({ providedIn: 'root' })
export class ContractsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/contracts`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ContractDto[]> {
    return this.http.get<ContractDto[]>(this.baseUrl);
  }

  create(request: CreateContractRequest): Observable<ContractDto> {
    return this.http.post<ContractDto>(this.baseUrl, request);
  }
}
