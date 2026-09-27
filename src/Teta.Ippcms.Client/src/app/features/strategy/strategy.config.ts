import { P } from '../../core/models';
import { CrudConfig } from '../../shared/crud-page.component';

export const PLANS: CrudConfig = {
  title: 'Strategic plans and APPs',
  subtitle: 'Strategic plan hierarchy: outcomes, objectives, APP indicators and quarterly targets. Approved plans are versioned (FR-STR-001..003).',
  list: 'strategy/plans',
  columns: [
    { key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'periodStart', label: 'From', type: 'date' },
    { key: 'periodEnd', label: 'To', type: 'date' }, { key: 'versionNumber', label: 'Version', type: 'number' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'objectiveCount', label: 'Objectives', type: 'number' },
    { key: 'indicatorCount', label: 'Indicators', type: 'number' }
  ],
  createPath: 'strategy/plans',
  createLabel: 'New plan',
  createPermissions: [P.strategyManage],
  fields: [
    { key: 'code', label: 'Code', required: true, maxLength: 40 }, { key: 'name', label: 'Name', required: true, wide: true },
    { key: 'description', label: 'Description', type: 'textarea' },
    { key: 'periodStart', label: 'Period start', type: 'date', required: true }, { key: 'periodEnd', label: 'Period end', type: 'date', required: true }
  ],
  rowLink: r => `/strategy/plans/${r.id}`
};
