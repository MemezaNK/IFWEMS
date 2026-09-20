import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TransactionPageDto, TransactionQuery } from '../models/transaction.models';

@Injectable({ providedIn: 'root' })
export class TransactionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/transactions`;

  constructor(private readonly http: HttpClient) {}

  query(query: TransactionQuery): Observable<TransactionPageDto> {
    let params = new HttpParams();
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    });
    return this.http.get<TransactionPageDto>(this.baseUrl, { params });
  }
}
