import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { P } from '../../core/models';
import { CrudConfig, CrudPageComponent } from '../../shared/crud-page.component';
import { holidayFields, referenceItemFields, retentionFields, settingFields, sodRuleFields, templateFields } from './admin-forms';

/** Segregation-of-duties rules, reference/master data, the working-day calendar, system settings, notification templates and record retention (FR-ADM-004/005/006/007/008). */
@Component({
  selector: 'teta-admin-config',
  standalone: true,
  imports: [MatTabsModule, CrudPageComponent],
  template: `
    <div style="padding-top: 12px">
      <mat-tab-group animationDuration="0">
        <mat-tab label="Segregation of duties"><teta-crud [config]="sodConfig" /></mat-tab>
        <mat-tab label="Reference data"><teta-crud [config]="refConfig" /></mat-tab>
        <mat-tab label="Working-day calendar"><teta-crud [config]="holidayConfig" /></mat-tab>
        <mat-tab label="System settings"><teta-crud [config]="settingConfig" /></mat-tab>
        <mat-tab label="Notification templates"><teta-crud [config]="templateConfig" /></mat-tab>
        <mat-tab label="Retention schedule"><teta-crud [config]="retentionConfig" /></mat-tab>
      </mat-tab-group>
    </div>
  `
})
export class AdminConfigComponent {
  sodConfig: CrudConfig = {
    title: 'Segregation-of-duties rules', list: 'admin/sod-rules', fields: sodRuleFields(),
    columns: [
      { key: 'code', label: 'Code' }, { key: 'description', label: 'Description' }, { key: 'entityType', label: 'Entity type' },
      { key: 'firstAction', label: 'First action' }, { key: 'secondAction', label: 'Second action' }, { key: 'mode', label: 'Mode' },
      { key: 'isActive', label: 'Active', type: 'bool' }
    ],
    createPath: 'admin/sod-rules', createLabel: 'New rule', createPermissions: [P.adminConfig], editPermissions: [P.adminConfig],
    updatePath: r => `admin/sod-rules/${r.id}`, emptyText: 'No segregation-of-duties rules defined.'
  };

  refConfig: CrudConfig = {
    title: 'Reference data', list: 'admin/reference-data', query: { activeOnly: false }, fields: referenceItemFields(),
    columns: [
      { key: 'category', label: 'Category' }, { key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'description', label: 'Description' },
      { key: 'sortOrder', label: 'Order', type: 'number' }, { key: 'isActive', label: 'Active', type: 'bool' }
    ],
    createPath: 'admin/reference-data', createLabel: 'New item', createPermissions: [P.adminConfig], editPermissions: [P.adminConfig],
    updatePath: r => `admin/reference-data/${r.id}`, emptyText: 'No reference data.'
  };

  holidayConfig: CrudConfig = {
    title: 'Public holidays / non-working days', list: 'admin/holidays', fields: holidayFields(),
    columns: [{ key: 'date', label: 'Date', type: 'date' }, { key: 'name', label: 'Name' }],
    createPath: 'admin/holidays', createLabel: 'New holiday', createPermissions: [P.adminConfig], editPermissions: [P.adminConfig],
    updatePath: r => `admin/holidays/${r.id}`, emptyText: 'No holidays configured.'
  };

  settingConfig: CrudConfig = {
    title: 'System settings', list: 'admin/settings', fields: settingFields(),
    columns: [
      { key: 'key', label: 'Key' }, { key: 'value', label: 'Value' }, { key: 'description', label: 'Description' },
      { key: 'category', label: 'Category' }, { key: 'isDefault', label: 'Default', type: 'bool' }
    ],
    editPermissions: [P.adminConfig], updatePath: r => `admin/settings/${r.key}`, emptyText: 'No settings.'
  };

  templateConfig: CrudConfig = {
    title: 'Notification templates', list: 'admin/notification-templates', fields: templateFields(),
    columns: [
      { key: 'code', label: 'Code' }, { key: 'subject', label: 'Subject' }, { key: 'sendEmail', label: 'Send email', type: 'bool' },
      { key: 'isActive', label: 'Active', type: 'bool' }
    ],
    editPermissions: [P.adminConfig], updatePath: r => `admin/notification-templates/${r.code}`, emptyText: 'No templates.'
  };

  retentionConfig: CrudConfig = {
    title: 'Record retention schedule', list: 'admin/retention', fields: retentionFields(),
    columns: [
      { key: 'recordClass', label: 'Record class' }, { key: 'retentionYears', label: 'Years', type: 'number' },
      { key: 'disposalAction', label: 'Disposal action' }, { key: 'legalReference', label: 'Legal reference' }, { key: 'isActive', label: 'Active', type: 'bool' }
    ],
    createPath: 'admin/retention', createLabel: 'New entry', createPermissions: [P.adminConfig], editPermissions: [P.adminConfig],
    updatePath: r => `admin/retention/${r.id}`, emptyText: 'No retention schedule defined.'
  };
}
