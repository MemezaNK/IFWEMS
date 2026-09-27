import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';

/** Portfolio → programme → project hierarchy (FR-POR-001). */
@Component({
  selector: 'teta-hierarchy',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule, StatusChipComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Portfolio hierarchy</h1>
        @if (canManage) {
          <button mat-stroked-button (click)="addPortfolio()"><mat-icon>add</mat-icon> Portfolio</button>
          <button mat-stroked-button (click)="addProgramme()"><mat-icon>add</mat-icon> Programme</button>
        }
      </div>
      @for (pf of nodes(); track pf.id) {
        <div class="card" style="margin-bottom: 12px">
          <h2>{{ pf.code }} – {{ pf.name }}</h2>
          @for (pg of pf.children; track pg.id) {
            <div style="margin: 8px 0 8px 16px">
              <strong>{{ pg.code }} – {{ pg.name }}</strong> <teta-status [value]="pg.status" />
              @for (pr of pg.children; track pr.id) {
                <div class="toolbar-row" style="margin: 4px 0 4px 24px">
                  <a [routerLink]="['/projects', pr.id]">{{ pr.code }}</a> <span>{{ pr.name }}</span>
                  <teta-status [value]="pr.status" /> <teta-status [value]="pr.health" />
                </div>
              } @empty { <div class="muted small" style="margin-left: 24px">No projects.</div> }
            </div>
          } @empty { <div class="muted">No programmes yet.</div> }
        </div>
      } @empty { <p class="muted">No portfolios have been set up.</p> }
    </div>
  `
})
export class HierarchyComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly nodes = signal<any[]>([]);

  get canManage(): boolean { return this.auth.has(P.portfolioManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('portfolio/hierarchy').subscribe(n => this.nodes.set(n));
  }

  async addPortfolio(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New portfolio', fields: [
      { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true },
      { key: 'description', label: 'Description', type: 'textarea' }, { key: 'ownerName', label: 'Owner' }] });
    if (v) this.api.post('portfolios', v).subscribe(() => { this.refs.invalidate('portfolios'); this.load(); });
  }

  async addProgramme(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New programme', fields: [
      { key: 'portfolioId', label: 'Portfolio', type: 'select', required: true, options: this.refs.portfolios() },
      { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true },
      { key: 'description', label: 'Description', type: 'textarea' }, { key: 'ownerUserId', label: 'Owner', type: 'select', options: this.refs.users() },
      { key: 'ownerName', label: 'Owner name' }] });
    if (v) this.api.post('programmes', v).subscribe(() => { this.refs.invalidate('programmes'); this.load(); });
  }
}
