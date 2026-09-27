import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';

/** APP performance: targets vs verified actuals; each figure traces to contributing projects (FR-REP-004). */
@Component({
  selector: 'teta-performance',
  standalone: true,
  imports: [DataTableComponent, FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <div class="page">
      <div class="page-header"><h1>APP performance</h1>
        <div class="subtitle">Verified actuals only count towards achievement; pending (unverified) results are shown separately.</div></div>
      <div class="toolbar-row">
        <mat-form-field subscriptSizing="dynamic"><mat-label>Financial year</mat-label><input matInput [(ngModel)]="fy" /></mat-form-field>
        <mat-form-field subscriptSizing="dynamic"><mat-label>Quarter</mat-label>
          <mat-select [(ngModel)]="quarter"><mat-option [value]="null">Full year</mat-option>
            @for (q of [1, 2, 3, 4]; track q) { <mat-option [value]="q">Q{{ q }}</mat-option> }</mat-select></mat-form-field>
        <button mat-flat-button color="primary" (click)="load()">Show</button>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="selected.set($event)" exportName="app-performance" />
      @if (selected(); as s) {
        <div class="card" style="margin-top: 16px">
          <h2>{{ s.code }} – contributing projects</h2>
          <teta-data-table [columns]="projectColumns" [rows]="s.contributingProjects" [filterable]="false" [paginate]="false" />
        </div>
      }
    </div>
  `
})
export class PerformanceComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly rows = signal<any[]>([]);
  readonly selected = signal<any | null>(null);
  fy = '2026/27';
  quarter: number | null = null;

  columns: Column[] = [
    { key: 'code', label: 'Indicator' }, { key: 'name', label: 'Name' }, { key: 'objectiveCode', label: 'Objective' },
    { key: 'target', label: 'Target', type: 'number' }, { key: 'verifiedActual', label: 'Verified actual', type: 'number' },
    { key: 'pendingActual', label: 'Pending', type: 'number' }, { key: 'forecast', label: 'Forecast', type: 'number' },
    { key: 'achievementPercent', label: 'Achievement', type: 'percent' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'forecastCommentary', label: 'Commentary' }
  ];
  projectColumns: Column[] = [
    { key: 'reference', label: 'Project' }, { key: 'name', label: 'Name' }, { key: 'verifiedContribution', label: 'Verified', type: 'number' },
    { key: 'pendingContribution', label: 'Pending', type: 'number' }, { key: 'resultCount', label: 'Results', type: 'number' }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<any[]>('strategy/performance', { financialYear: this.fy, quarter: this.quarter }).subscribe(r => this.rows.set(r));
  }
}
