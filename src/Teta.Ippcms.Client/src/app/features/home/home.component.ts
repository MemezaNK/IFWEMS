import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { entityLink } from '../common/links';

/** Role-specific landing page: approvals, actions, deadlines, alerts, my projects and recent records (NFR-016, SRS §13). */
@Component({
  selector: 'teta-home',
  standalone: true,
  imports: [KpiCardsComponent, StatusChipComponent, RouterLink, DatePipe, MatButtonModule, MatIconModule],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Good day, {{ home()?.displayName }}</h1>
        <span class="spacer"></span>
        <button mat-stroked-button (click)="exportMyActivityReport()"><mat-icon>download</mat-icon> My activity report (PDF)</button>
      </div>
      @if (home(); as h) {
        <teta-kpis [kpis]="h.kpis" />
        <div class="grid cols-2">
          <div class="card">
            <h2>Approvals waiting for me ({{ h.approvals.length }})</h2>
            @for (a of h.approvals.slice(0, 8); track a.taskId) {
              <div class="toolbar-row" style="border-bottom: 1px solid #eef1f4; padding: 6px 0; margin: 0">
                <a [routerLink]="link(a.entityType, a.entityId)">{{ a.entityReference }} – {{ a.title }}</a>
                <span class="spacer"></span>
                <span class="small muted">{{ a.stepName }}</span>
                <teta-status [value]="a.isOverdue ? 'Overdue' : 'Pending'" />
              </div>
            } @empty { <p class="muted">Nothing waiting for you.</p> }
            @if (h.approvals.length) { <a mat-button routerLink="/inbox">Open inbox</a> }
          </div>
          <div class="card">
            <h2>My actions ({{ h.actions.length }})</h2>
            @for (a of h.actions.slice(0, 10); track a.id) {
              <div class="toolbar-row" style="border-bottom: 1px solid #eef1f4; padding: 6px 0; margin: 0">
                <span class="small muted" style="width: 130px">{{ a.type }}</span>
                <a [routerLink]="a.link">{{ a.reference }}</a>
                <span class="small" style="flex: 1">{{ a.description }}</span>
                <span class="small">{{ a.dueDate }}</span>
                @if (a.isOverdue) { <teta-status value="Overdue" /> }
              </div>
            } @empty { <p class="muted">No open actions assigned to you.</p> }
          </div>
          <div class="card">
            <h2>Upcoming deadlines</h2>
            @for (d of h.deadlines.slice(0, 10); track d.reference + d.type) {
              <div class="toolbar-row" style="border-bottom: 1px solid #eef1f4; padding: 6px 0; margin: 0">
                <span class="small muted" style="width: 130px">{{ d.type }}</span>
                <a [routerLink]="d.link">{{ d.reference }}</a>
                <span class="small" style="flex: 1">{{ d.description }}</span>
                <span class="small">{{ d.dueDate }}</span>
                <teta-status [value]="d.daysRemaining < 0 ? 'Overdue' : d.daysRemaining <= 7 ? 'Amber' : 'Green'" />
              </div>
            } @empty { <p class="muted">No deadlines in the next 14 days.</p> }
          </div>
          <div class="card">
            <h2>Alerts ({{ h.unreadAlerts }} unread)</h2>
            @for (n of h.alerts; track n.id) {
              <div style="border-bottom: 1px solid #eef1f4; padding: 6px 0" [style.font-weight]="n.isRead ? 400 : 500">
                <div>{{ n.title }}</div>
                <div class="small muted">{{ n.createdAtUtc | date: 'yyyy-MM-dd HH:mm' }}</div>
              </div>
            } @empty { <p class="muted">No alerts.</p> }
            <a mat-button routerLink="/notifications">All notifications</a>
          </div>
          <div class="card">
            <h2>My projects</h2>
            @for (p of h.myProjects; track p.id) {
              <div class="toolbar-row" style="border-bottom: 1px solid #eef1f4; padding: 6px 0; margin: 0">
                <a [routerLink]="['/projects', p.id]">{{ p.reference }}</a>
                <span style="flex: 1">{{ p.name }}</span>
                <teta-status [value]="p.status" /> <teta-status [value]="p.health" />
              </div>
            } @empty { <p class="muted">You are not managing or sponsoring any active project.</p> }
          </div>
          <div class="card">
            <h2>Recently worked on</h2>
            @for (r of h.recent; track r.entityId) {
              <div class="toolbar-row" style="border-bottom: 1px solid #eef1f4; padding: 6px 0; margin: 0">
                <span class="small muted" style="width: 130px">{{ r.entityType }}</span>
                <a [routerLink]="r.link">{{ r.action }}</a>
                <span class="spacer"></span>
                <span class="small muted">{{ r.occurredAtUtc | date: 'yyyy-MM-dd HH:mm' }}</span>
              </div>
            } @empty { <p class="muted">Nothing yet.</p> }
          </div>
        </div>
      }
    </div>
  `
})
export class HomeComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly home = signal<any | null>(null);
  readonly link = entityLink;

  ngOnInit(): void {
    if (this.auth.isSupplier()) {
      this.router.navigateByUrl('/portal');
      return;
    }
    this.api.get('home').subscribe(h => this.home.set(h));
  }

  exportMyActivityReport(): void {
    this.api.download('me/activity-report/export').subscribe(r => ApiService.saveBlob(r, 'my-activity-report.pdf'));
  }
}
