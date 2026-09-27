import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { committeeFields, meetingFields, memberFields } from './procurement-forms';

/** Bid committees, time-bound membership, meetings and conflict-of-interest declarations (FR-SCM-003, BR safeguards). */
@Component({
  selector: 'teta-procurement-committee',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px">
      <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Committees</h2>
        @if (canManage) { <button mat-stroked-button (click)="addCommittee()"><mat-icon>add</mat-icon> New committee</button> }
      </div>
      @for (c of committees(); track c.id) {
        <div class="card" style="margin-bottom: 12px">
          <div class="toolbar-row"><strong>{{ c.name }}</strong> <span class="muted small">({{ c.type }}, quorum {{ c.quorum }})</span>
            <span class="spacer"></span>
            @if (canManage) { <button mat-stroked-button (click)="addMember(c)"><mat-icon>person_add</mat-icon> Add member</button>
              <button mat-stroked-button (click)="addMeeting(c)"><mat-icon>event</mat-icon> Record meeting</button> }
          </div>
          <teta-data-table [columns]="memberColumns" [rows]="c.members" [actions]="canManage ? memberActions : undefined" [filterable]="false"
                           [paginate]="false" [exportable]="false" emptyText="No members yet." />
          <ng-template #memberActions let-row>
            @if (row.hasAccessToday) { <button mat-icon-button (click)="endMembership(c, row)" title="End membership"><mat-icon>person_remove</mat-icon></button> }
          </ng-template>
          @if ((meetings[c.id] ?? []).length) {
            <h3 class="small muted" style="margin-top:12px">Meetings</h3>
            <table class="full-width small">
              <tr class="muted"><td>When</td><td>Agenda</td><td>Attendees</td><td>Quorum met</td></tr>
              @for (m of meetings[c.id]; track m.id) {
                <tr><td>{{ m.meetingAtUtc }}</td><td>{{ m.agenda }}</td><td>{{ m.attendeesCount }}</td><td>{{ m.quorumMet ? 'Yes' : 'No' }}</td></tr>
              }
            </table>
          }
        </div>
      } @empty { <p class="muted">No committees created yet.</p> }

      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Conflict-of-interest declarations</h2>
          @if (canDeclare && !detail.hasDeclared) { <button mat-flat-button color="primary" (click)="declare()">Declare</button> }
        </div>
        <teta-data-table [columns]="declColumns" [rows]="declarations()" [filterable]="false" [paginate]="false" [exportable]="false"
                         emptyText="No declarations recorded yet." />
      </div>
    </div>
  `
})
export class ProcurementCommitteeComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly committees = signal<any[]>([]);
  readonly declarations = signal<any[]>([]);
  meetings: Record<string, any[] | undefined> = {};

  memberColumns: Column[] = [
    { key: 'name', label: 'Name' }, { key: 'role', label: 'Role' }, { key: 'accessFrom', label: 'From', type: 'date' },
    { key: 'accessTo', label: 'To', type: 'date' }, { key: 'hasAccessToday', label: 'Active', type: 'bool' },
    { key: 'declared', label: 'Declared', type: 'bool' }, { key: 'hasConflict', label: 'Conflict', type: 'bool' }
  ];
  declColumns: Column[] = [
    { key: 'declarantName', label: 'Declarant' }, { key: 'hasConflict', label: 'Conflict', type: 'bool' },
    { key: 'conflictDetails', label: 'Details' }, { key: 'confidentialityAccepted', label: 'Confidentiality', type: 'bool' },
    { key: 'declaredAtUtc', label: 'Declared', type: 'datetime' }
  ];

  get canManage(): boolean { return this.auth.has(P.procurementManage); }
  get canDeclare(): boolean { return this.auth.hasAny(P.procurementEvaluate, P.procurementManage); }

  ngOnChanges(): void {
    this.committees.set(this.detail.committees ?? []);
    this.api.get<any[]>(`procurement/${this.detail.summary.id}/declarations`).subscribe(r => this.declarations.set(r));
    for (const c of this.committees()) {
      this.api.get<any[]>(`procurement/committees/${c.id}/meetings`).subscribe(r => (this.meetings[c.id] = r));
    }
  }

  async addCommittee(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New committee', fields: committeeFields() }, '620px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/committees`, v).subscribe(() => this.changed.emit());
  }

  async addMember(c: any): Promise<void> {
    const v = await openForm(this.dialog, { title: `Add member – ${c.name}`, fields: memberFields(this.refs) }, '620px');
    if (v) this.api.post(`procurement/committees/${c.id}/members`, v).subscribe(() => this.changed.emit());
  }

  endMembership(c: any, row: any): void {
    this.api.post(`procurement/committees/${c.id}/members/${row.id}/end`).subscribe(() => this.changed.emit());
  }

  async addMeeting(c: any): Promise<void> {
    const v = await openForm(this.dialog, { title: `Record meeting – ${c.name}`, fields: meetingFields() }, '760px');
    if (v) this.api.post(`procurement/committees/${c.id}/meetings`, v).subscribe(() => this.changed.emit());
  }

  async declare(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Conflict-of-interest declaration', fields: [
      { key: 'hasConflict', label: 'I have a conflict of interest in this process', type: 'checkbox' },
      { key: 'conflictDetails', label: 'Conflict details', type: 'textarea' },
      { key: 'confidentialityAccepted', label: 'I accept the confidentiality undertaking', type: 'checkbox', required: true }
    ] }, '620px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/declarations`, v).subscribe(() => this.changed.emit());
  }
}
