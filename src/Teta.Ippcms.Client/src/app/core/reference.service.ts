import { Injectable, inject } from '@angular/core';
import { Observable, map, of, shareReplay } from 'rxjs';
import { ApiService } from './api.service';

export interface Option { value: any; label: string; }

/** Cached look-ups for drop-downs: reference data, users, programmes, projects, suppliers. */
@Injectable({ providedIn: 'root' })
export class ReferenceService {
  private readonly api = inject(ApiService);
  private readonly cache = new Map<string, Observable<Option[]>>();

  private cached(key: string, factory: () => Observable<Option[]>): Observable<Option[]> {
    if (!this.cache.has(key)) this.cache.set(key, factory().pipe(shareReplay(1)));
    return this.cache.get(key)!;
  }

  invalidate(prefix?: string): void {
    for (const key of [...this.cache.keys()]) if (!prefix || key.startsWith(prefix)) this.cache.delete(key);
  }

  category(category: string): Observable<Option[]> {
    return this.cached('ref:' + category, () => this.api.get<any[]>('admin/reference-data', { category })
      .pipe(map(items => items.map(i => ({ value: i.name, label: i.name })))));
  }

  users(role?: string): Observable<Option[]> {
    return this.cached('users:' + (role ?? ''), () => this.api.get<any[]>('security/users/lookup', { roleCode: role })
      .pipe(map(items => items.map(u => ({ value: u.id, label: `${u.displayName} (${u.username})` })))));
  }

  programmes(): Observable<Option[]> {
    return this.cached('programmes', () => this.api.get<any[]>('programmes')
      .pipe(map(items => items.map(p => ({ value: p.id, label: `${p.code} – ${p.name}` })))));
  }

  portfolios(): Observable<Option[]> {
    return this.cached('portfolios', () => this.api.get<any[]>('portfolios')
      .pipe(map(items => items.map(p => ({ value: p.id, label: `${p.code} – ${p.name}` })))));
  }

  projects(): Observable<Option[]> {
    return this.cached('projects', () => this.api.get<any>('projects', { pageSize: 500 })
      .pipe(map(page => (page.items as any[]).map(p => ({ value: p.id, label: `${p.reference} – ${p.name}` })))));
  }

  suppliers(): Observable<Option[]> {
    return this.cached('suppliers', () => this.api.get<any>('suppliers', { pageSize: 500 })
      .pipe(map(page => (page.items as any[]).map(s => ({ value: s.id, label: `${s.supplierNumber} – ${s.legalName}` })))));
  }

  enumOptions(values: string[]): Observable<Option[]> {
    return of(values.map(v => ({ value: v, label: v.replace(/([a-z])([A-Z])/g, '$1 $2') })));
  }
}
