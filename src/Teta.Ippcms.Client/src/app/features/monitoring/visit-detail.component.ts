import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { DocumentsPanelComponent } from '../../shared/documents-panel.component';
import { Field, openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';

/** A single monitoring visit: template-driven capture, findings raised and evidence (FR-ME-003/004/005). */
@Component({
  selector: 'teta-visit-detail',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, DocumentsPanelComponent, StatusChipComponent],
  template: `
    @if (visit(); as v) {
      <div class="page">
        <div class="page-header">
          <h1>{{ v.number }} – {{ v.projectReference }}</h1>
          <teta-status [value]="v.status" />
          @if (canManage && v.status !== 'Completed') { <button mat-flat-button color="primary" (click)="capture()"><mat-icon>edit_note</mat-icon> Capture</button> }
        </div>
        <div class="grid cols-2">
          <div class="card">
            <dl class="dl small">
              <dt>Type</dt><dd>{{ v.type }}</dd><dt>Scheduled</dt><dd>{{ v.scheduledDate }}</dd><dt>Actual date</dt><dd>{{ v.visitDate ?? '—' }}</dd>
              <dt>Officials</dt><dd>{{ v.officials ?? '—' }}</dd><dt>Location</dt><dd>{{ v.location ?? '—' }}</dd>
              <dt>Summary</dt><dd>{{ v.summary ?? '—' }}</dd><dt>Outcome</dt><dd>{{ v.outcome ?? '—' }}</dd>
            </dl>
          </div>
          <div class="card">
            <h2 style="margin-top: 0">Template responses</h2>
            @if (v.fields.length) {
              <dl class="dl small">
                @for (f of v.fields; track f.key) { <dt>{{ f.label }}</dt><dd>{{ v.responses[f.key] ?? '—' }}</dd> }
              </dl>
            } @else { <p class="muted">No template assigned to this visit.</p> }
          </div>
        </div>
        <div class="card" style="margin-top: 16px">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Findings</h2>
            @if (canManage) { <button mat-stroked-button (click)="addFinding()"><mat-icon>add</mat-icon> Raise finding</button> }
          </div>
          <teta-data-table [columns]="findingColumns" [rows]="findings()" [filterable]="false" [paginate]="false" [exportable]="false"
                           emptyText="No findings raised for this visit." />
        </div>
        <div class="card" style="margin-top: 16px"><teta-documents parentType="MonitoringVisit" [parentId]="v.id" [evidenceMode]="true" /></div>
      </div>
    }
  `
})
export class VisitDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly visit = signal<any | null>(null);
  readonly findings = signal<any[]>([]);

  findingColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'description', label: 'Description' }, { key: 'severity', label: 'Severity', type: 'status' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'openActions', label: 'Open actions', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.meManage); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get(`me/visits/${this.id}`).subscribe((v: any) => {
      this.visit.set(v);
      this.api.get<any[]>('me/findings', { projectId: v.projectId }).subscribe(all => this.findings.set(all.filter(f => f.visitId === v.id)));
    });
  }

  private templateField(f: any): Field {
    const base = { key: f.key, label: f.label, required: f.required };
    switch (f.type) {
      case 'textarea': return { ...base, type: 'textarea', wide: true };
      case 'number': return { ...base, type: 'number' };
      case 'date': return { ...base, type: 'date' };
      case 'select': return { ...base, type: 'select', options: (f.options ?? []).map((o: string) => ({ value: o, label: o })) };
      case 'yesno': return { ...base, type: 'select', options: [{ value: 'Yes', label: 'Yes' }, { value: 'No', label: 'No' }] };
      case 'rating': return { ...base, type: 'select', options: ['1', '2', '3', '4', '5'].map(v => ({ value: v, label: v })) };
      default: return { ...base, type: 'text' };
    }
  }

  async capture(): Promise<void> {
    const v = this.visit()!;
    const fixed: Field[] = [
      { key: 'visitDate', label: 'Actual visit date', type: 'date', required: true },
      { key: 'officials', label: 'Officials attending', required: true },
      { key: 'location', label: 'Location' }, { key: 'latitude', label: 'Latitude', type: 'number' }, { key: 'longitude', label: 'Longitude', type: 'number' },
      { key: 'summary', label: 'Summary', type: 'textarea', required: true, wide: true }, { key: 'outcome', label: 'Outcome', type: 'textarea', required: true, wide: true },
      { key: 'complete', label: 'Mark visit as complete', type: 'checkbox' }
    ];
    const templateFields = v.fields.map((f: any) => this.templateField(f));
    const value: Record<string, unknown> = { officials: v.officials, location: v.location, summary: v.summary, outcome: v.outcome, ...v.responses };
    const result = await openForm<Record<string, any>>(this.dialog, { title: 'Capture visit', fields: [...fixed, ...templateFields], value }, '860px');
    if (!result) return;
    const responses: Record<string, string | null> = {};
    for (const f of v.fields) responses[f.key] = result[f.key] === null || result[f.key] === undefined ? null : String(result[f.key]);
    this.api.post(`me/visits/${v.id}/capture`, {
      visitDate: result['visitDate'], officials: result['officials'], location: result['location'], latitude: result['latitude'], longitude: result['longitude'],
      responses, summary: result['summary'], outcome: result['outcome'], complete: !!result['complete']
    }).subscribe(() => {
      this.snack.open('Visit captured.', 'OK', { duration: 3000 });
      this.load();
    });
  }

  async addFinding(): Promise<void> {
    const v = this.visit()!;
    const value = await openForm(this.dialog, { title: 'Raise finding', fields: [
      { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
      { key: 'severity', label: 'Severity', type: 'select', required: true, options: [{ value: 'Low', label: 'Low' }, { value: 'Medium', label: 'Medium' },
        { value: 'High', label: 'High' }, { value: 'Critical', label: 'Critical' }] },
      { key: 'rootCause', label: 'Root cause', type: 'textarea' }
    ] }, '780px');
    if (value) this.api.post('me/findings', { projectId: v.projectId, visitId: v.id, ...value }).subscribe(() => this.load());
  }
}
