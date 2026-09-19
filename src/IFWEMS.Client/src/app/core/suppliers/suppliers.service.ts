import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SupplierDto } from '../models/supplier.models';
import { SupplierRiskProfileDto } from '../models/supplier-risk.models';

export interface CreateSupplierRequest {
  supplierCode: string;
  name: string;
  registrationNumber: string | null;
}

@Injectable({ providedIn: 'root' })
export class SuppliersService {
  private readonly baseUrl = `${environment.apiBaseUrl}/suppliers`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<SupplierDto[]> {
    return this.http.get<SupplierDto[]>(this.baseUrl);
  }

  create(request: CreateSupplierRequest): Observable<SupplierDto> {
    return this.http.post<SupplierDto>(this.baseUrl, request);
  }

  getRiskProfile(id: string): Observable<SupplierRiskProfileDto> {
    return this.http.get<SupplierRiskProfileDto>(`${this.baseUrl}/${id}/risk-profile`);
  }
}
