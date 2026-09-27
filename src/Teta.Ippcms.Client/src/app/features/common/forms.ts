import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

const opts = (values: string[]) => values.map(v => ({ value: v, label: v.replace(/([a-z])([A-Z])/g, '$1 $2') }));
export const SEVERITIES = ['Low', 'Medium', 'High', 'Critical'];

export function issueFields(refs: ReferenceService, editing: boolean): Field[] {
  const fields: Field[] = [
    { key: 'title', label: 'Title', required: true, wide: true }, { key: 'description', label: 'Description', type: 'textarea', required: true },
    { key: 'severity', label: 'Severity', type: 'select', required: true, options: opts(SEVERITIES) },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name', required: true },
    { key: 'action', label: 'Action', type: 'textarea' }, { key: 'dueDate', label: 'Due date', type: 'date' }
  ];
  if (editing) {
    fields.push({ key: 'status', label: 'Status', type: 'select', options: opts(['Open', 'InProgress', 'Escalated', 'Resolved', 'Closed']) },
      { key: 'resolution', label: 'Resolution', type: 'textarea' });
  }
  return fields;
}

export function riskFields(refs: ReferenceService): Field[] {
  const scale = [1, 2, 3, 4, 5].map(n => ({ value: n, label: String(n) }));
  return [
    { key: 'title', label: 'Risk title', required: true, wide: true },
    { key: 'cause', label: 'Cause', type: 'textarea', required: true }, { key: 'event', label: 'Event', type: 'textarea', required: true },
    { key: 'consequence', label: 'Consequence', type: 'textarea', required: true },
    { key: 'category', label: 'Category', type: 'select', options: refs.category('RiskCategory') },
    { key: 'inherentLikelihood', label: 'Inherent likelihood (1-5)', type: 'select', required: true, options: scale },
    { key: 'inherentImpact', label: 'Inherent impact (1-5)', type: 'select', required: true, options: scale },
    { key: 'residualLikelihood', label: 'Residual likelihood (1-5)', type: 'select', required: true, options: scale },
    { key: 'residualImpact', label: 'Residual impact (1-5)', type: 'select', required: true, options: scale },
    { key: 'ownerUserId', label: 'Risk owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name', required: true },
    { key: 'reviewDate', label: 'Next review', type: 'date', required: true }
  ];
}

export function enumOptions(values: string[]) {
  return opts(values);
}
