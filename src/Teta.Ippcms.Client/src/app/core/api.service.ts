import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/** Thin wrapper around HttpClient that prefixes the versioned API base URL and drops empty query values. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  readonly base = environment.apiBaseUrl;

  url(path: string): string {
    return `${this.base}/${path.replace(/^\/+/, '')}`;
  }

  params(query?: Record<string, unknown>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value === null || value === undefined || value === '') continue;
      params = params.set(key, value instanceof Date ? value.toISOString() : String(value));
    }
    return params;
  }

  get<T = any>(path: string, query?: Record<string, unknown>): Observable<T> {
    return this.http.get<T>(this.url(path), { params: this.params(query) });
  }

  post<T = any>(path: string, body: unknown = {}, query?: Record<string, unknown>, headers?: Record<string, string>): Observable<T> {
    return this.http.post<T>(this.url(path), body, { params: this.params(query), headers });
  }

  put<T = any>(path: string, body: unknown = {}, query?: Record<string, unknown>): Observable<T> {
    return this.http.put<T>(this.url(path), body, { params: this.params(query) });
  }

  delete<T = any>(path: string): Observable<T> {
    return this.http.delete<T>(this.url(path));
  }

  upload<T = any>(path: string, form: FormData, query?: Record<string, unknown>): Observable<T> {
    return this.http.post<T>(this.url(path), form, { params: this.params(query) });
  }

  download(path: string, query?: Record<string, unknown>): Observable<HttpResponse<Blob>> {
    return this.http.get(this.url(path), { params: this.params(query), responseType: 'blob', observe: 'response' });
  }

  /** Saves a blob response using the server-supplied file name. */
  static saveBlob(response: HttpResponse<Blob>, fallbackName: string): void {
    const disposition = response.headers.get('content-disposition') ?? '';
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    const name = match ? decodeURIComponent(match[1]) : fallbackName;
    const url = URL.createObjectURL(response.body!);
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
