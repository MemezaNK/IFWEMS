import { Component, Input, OnChanges, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, map } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { Column, DataTableComponent } from './data-table.component';
import { Field, confirmAction, openForm } from './form-dialog.component';

export interface RowAction {
  label: string;
  icon: string;
  permissions?: string[];
  visible?: (row: any) => boolean;
  /** Returns an observable to run; the list reloads on success. */
  run: (row: any, ctx: CrudContext) => Promise<Observable<unknown> | void> | Observable<unknown> | void;
}

export interface CrudContext {
  api: ApiService;
  dialog: MatDialog;
  router: Router;
  reload: () => void;
}

export interface CrudConfig {
  title: string;
  subtitle?: string;
  /** GET path returning an array (or a paged result when paged = true). */
  list: string;
  query?: Record<string, unknown>;
  paged?: boolean;
  columns: Column[];
  fields?: Field[];
  createPath?: string;
  createLabel?: string;
  createPermissions?: string[];
  updatePath?: (row: any) => string;
  updateMethod?: 'put' | 'post';
  editPermissions?: string[];
  toForm?: (row: any) => Record<string, unknown>;
  toRequest?: (value: any, row?: any) => unknown;
  rowLink?: (row: any) => string;
  rowActions?: RowAction[];
  emptyText?: string;
}

/** Configurable list + create/edit page used for registers and configuration lists. */
@Component({
  selector: 'teta-crud',
  standalone: true,
  imports: [DataTableComponent, MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    @if (config) {
      <div [class.page]="standalone">
        <div class="page-header">
          @if (standalone) { <h1>{{ config.title }}</h1> } @else { <h2 style="margin: 0; flex: 1">{{ config.title }}</h2> }
          @if (config.createPath && can(config.createPermissions)) {
            <button mat-flat-button color="primary" (click)="edit()"><mat-icon>add</mat-icon> {{ config.createLabel ?? 'New' }}</button>
          }
          @if (config.subtitle) { <div class="subtitle">{{ config.subtitle }}</div> }
        </div>
        <teta-data-table [columns]="config.columns" [rows]="rows()" [actions]="hasActions ? actions : undefined"
                         (rowClick)="open($event)" [emptyText]="config.emptyText ?? 'No records.'" [exportName]="config.title" />
        <ng-template #actions let-row>
          @for (a of config.rowActions ?? []; track a.label) {
            @if (can(a.permissions) && (!a.visible || a.visible(row))) {
              <button mat-icon-button [matTooltip]="a.label" (click)="runAction(a, row)"><mat-icon>{{ a.icon }}</mat-icon></button>
            }
          }
          @if (config.updatePath && config.fields && can(config.editPermissions ?? config.createPermissions)) {
            <button mat-icon-button matTooltip="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
          }
        </ng-template>
      </div>
    }
  `
})
export class CrudPageComponent implements OnInit, OnChanges {
  @Input() config?: CrudConfig;
  @Input() standalone = false;

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);

  get hasActions(): boolean {
    return !!(this.config?.rowActions?.length || (this.config?.updatePath && this.config.fields));
  }

  ngOnInit(): void {
    const fromRoute = this.route.snapshot.data['crud'] as CrudConfig | undefined;
    if (!this.config && fromRoute) {
      this.config = fromRoute;
      this.standalone = true;
    }
    this.load();
  }

  ngOnChanges(): void {
    this.load();
  }

  can(permissions?: string[]): boolean {
    return !permissions || this.auth.hasAny(...permissions);
  }

  load(): void {
    const c = this.config;
    if (!c) return;
    const call = this.api.get<any>(c.list, { ...(c.query ?? {}), ...(c.paged ? { pageSize: 500 } : {}) });
    call.pipe(map(r => (c.paged ? r.items : r) as any[])).subscribe(rows => this.rows.set(rows));
  }

  open(row: any): void {
    if (this.config?.rowLink) this.router.navigateByUrl(this.config.rowLink(row));
  }

  async edit(row?: any): Promise<void> {
    const c = this.config!;
    if (!c.fields) return;
    const value = await openForm(this.dialog, {
      title: row ? `Edit ${c.title.replace(/s$/, '').toLowerCase()}` : (c.createLabel ?? `New ${c.title.replace(/s$/, '').toLowerCase()}`),
      fields: c.fields,
      value: row ? (c.toForm ? c.toForm(row) : row) : undefined
    });
    if (!value) return;
    const body = c.toRequest ? c.toRequest(value, row) : value;
    const call = row
      ? (c.updateMethod === 'post' ? this.api.post(c.updatePath!(row), body) : this.api.put(c.updatePath!(row), body))
      : this.api.post(c.createPath!, body);
    call.subscribe(() => {
      this.snack.open('Saved.', 'OK', { duration: 3000 });
      this.load();
    });
  }

  async runAction(action: RowAction, row: any): Promise<void> {
    const ctx: CrudContext = { api: this.api, dialog: this.dialog, router: this.router, reload: () => this.load() };
    const result = await action.run(row, ctx);
    if (result && 'subscribe' in result) {
      result.subscribe(() => {
        this.snack.open(`${action.label}: done.`, 'OK', { duration: 3000 });
        this.load();
      });
    }
  }
}

export { confirmAction };
