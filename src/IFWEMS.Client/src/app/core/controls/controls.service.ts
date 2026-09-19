import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ControlDto } from '../models/control.models';

@Injectable({ providedIn: 'root' })
export class ControlsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/controls`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ControlDto[]> {
    return this.http.get<ControlDto[]>(this.baseUrl);
  }
}
