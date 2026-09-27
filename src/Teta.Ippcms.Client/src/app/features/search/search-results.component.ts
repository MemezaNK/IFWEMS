import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { ApiService } from '../../core/api.service';

/** Global search across projects, procurements, contracts, suppliers, invoices and documents (SRS §13). */
@Component({
  selector: 'teta-search-results',
  standalone: true,
  imports: [RouterLink, MatButtonModule],
  template: `
    <div class="page">
      <div class="page-header"><h1>Search results for "{{ query() }}"</h1></div>
      @if (response(); as r) {
        <div class="toolbar-row" style="margin-bottom: 12px">
          <button mat-stroked-button [color]="!activeType() ? 'primary' : undefined" (click)="filter(undefined)">All ({{ r.results.length }})</button>
          @for (t of typeKeys(r.countsByType); track t) {
            <button mat-stroked-button [color]="activeType() === t ? 'primary' : undefined" (click)="filter(t)">{{ t }} ({{ r.countsByType[t] }})</button>
          }
        </div>
        @for (item of filtered(); track item.type + item.id) {
          <a class="card" [routerLink]="item.link" style="display: block; margin-bottom: 8px; text-decoration: none; color: inherit">
            <div class="toolbar-row" style="margin: 0">
              <span class="small muted" style="width: 140px">{{ item.type }}</span>
              <div style="flex: 1">
                <div style="font-weight: 500">{{ item.reference }} – {{ item.title }}</div>
                @if (item.subtitle) { <div class="small muted">{{ item.subtitle }}</div> }
              </div>
            </div>
          </a>
        } @empty { <p class="muted">No results found for this search.</p> }
      }
    </div>
  `
})
export class SearchResultsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  readonly query = signal('');
  readonly response = signal<any | null>(null);
  readonly activeType = signal<string | undefined>(undefined);

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(p => {
      const q = p.get('q') ?? '';
      this.query.set(q);
      this.activeType.set(undefined);
      if (q) this.api.get<any>('search', { q, limit: 200 }).subscribe(r => this.response.set(r));
    });
  }

  typeKeys(counts: Record<string, number>): string[] {
    return Object.keys(counts ?? {});
  }

  filter(type: string | undefined): void {
    this.activeType.set(type);
  }

  filtered(): any[] {
    const r = this.response();
    if (!r) return [];
    const t = this.activeType();
    return t ? r.results.filter((x: any) => x.type === t) : r.results;
  }
}
