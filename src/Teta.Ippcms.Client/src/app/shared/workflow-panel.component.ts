import { DatePipe } from '@angular/common';
import { Component, Input, OnChanges, inject } from '@angular/core';
import { ApiService } from '../core/api.service';
import { StatusChipComponent } from './status-chip.component';

/** Approval history for a record: every workflow version, step, decision, actor (incl. "on behalf of") and comment. */
@Component({
  selector: 'teta-workflow',
  standalone: true,
  imports: [DatePipe, StatusChipComponent],
  template: `
    @for (wf of instances; track wf.id) {
      <div class="card" style="margin-bottom: 12px">
        <div class="toolbar-row">
          <strong>{{ wf.definitionCode }} v{{ wf.definitionVersion }}</strong>
          <teta-status [value]="wf.state" />
          <span class="muted small">Started {{ wf.startedAtUtc | date: 'yyyy-MM-dd HH:mm' }} by {{ wf.startedBy }}</span>
        </div>
        <table class="full-width small">
          <tr class="muted"><td>Step</td><td>Role</td><td>Decision</td><td>By</td><td>When</td><td>Due</td><td>Comment</td></tr>
          @for (t of wf.tasks; track t.id) {
            <tr>
              <td>{{ t.stepName }}</td>
              <td>{{ t.assignedRole }}@if (t.escalatedToRole) { → {{ t.escalatedToRole }} }</td>
              <td><teta-status [value]="t.isOverdue && t.decision === 'Pending' ? 'Overdue' : t.decision" /></td>
              <td>{{ t.decidedBy }}@if (t.onBehalfOf) { <span class="muted"> (for {{ t.onBehalfOf }})</span> }</td>
              <td>{{ t.decidedAtUtc | date: 'yyyy-MM-dd HH:mm' }}</td>
              <td>{{ t.dueAtUtc | date: 'yyyy-MM-dd' }}</td>
              <td>{{ t.comment }}</td>
            </tr>
          }
        </table>
      </div>
    } @empty {
      <p class="muted">No approvals have been requested for this record.</p>
    }
  `
})
export class WorkflowPanelComponent implements OnChanges {
  @Input({ required: true }) entityType!: string;
  @Input({ required: true }) entityId!: string;
  @Input() refresh = 0;
  private readonly api = inject(ApiService);
  instances: any[] = [];

  ngOnChanges(): void {
    this.api.get<any[]>('workflows', { entityType: this.entityType, entityId: this.entityId }).subscribe(r => (this.instances = r));
  }
}
