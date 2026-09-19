import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DocumentDto } from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/documents`;

  constructor(private readonly http: HttpClient) {}

  getAll(caseId?: string): Observable<DocumentDto[]> {
    const url = caseId ? `${this.baseUrl}?caseId=${caseId}` : this.baseUrl;
    return this.http.get<DocumentDto[]>(url);
  }

  upload(file: File, documentType: string, confidentialityClassification: string, caseId?: string): Observable<DocumentDto> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentType', documentType);
    formData.append('confidentialityClassification', confidentialityClassification);
    if (caseId) {
      formData.append('caseId', caseId);
    }
    return this.http.post<DocumentDto>(this.baseUrl, formData);
  }
}
