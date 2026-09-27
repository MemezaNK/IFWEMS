import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

export function fromAwardFields(): Field[] {
  return [
    { key: 'title', label: 'Contract title', required: true, wide: true },
    { key: 'startDate', label: 'Start date', type: 'date', required: true }, { key: 'endDate', label: 'End date', type: 'date', required: true },
    { key: 'contractManagerName', label: 'Contract manager' }, { key: 'poReference', label: 'PO reference' }
  ];
}

export function nonBidFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'supplierId', label: 'Supplier', type: 'select', required: true, options: refs.suppliers(), wide: true },
    { key: 'exceptionId', label: 'Approved exception / deviation ID', required: true, hint: 'From Procurement › Deviations & exceptions', wide: true },
    { key: 'title', label: 'Title', required: true, wide: true }, { key: 'value', label: 'Value (R)', type: 'number', required: true, min: 0 },
    { key: 'startDate', label: 'Start date', type: 'date', required: true }, { key: 'endDate', label: 'End date', type: 'date', required: true },
    { key: 'contractManagerName', label: 'Contract manager' }, { key: 'poReference', label: 'PO reference' }
  ];
}

export function updateContractFields(): Field[] {
  return [
    { key: 'title', label: 'Title', required: true, wide: true },
    { key: 'contractManagerName', label: 'Contract manager' }, { key: 'poReference', label: 'PO reference' }
  ];
}

export function obligationFields(refs: ReferenceService): Field[] {
  return [
    { key: 'type', label: 'Type', type: 'select', required: true, options: refs.enumOptions(['Deliverable', 'Milestone', 'Kpi', 'Sla', 'Reporting', 'Compliance']) },
    { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name (if not a system user)' },
    { key: 'dueDate', label: 'Due date', type: 'date', required: true },
    { key: 'evidenceRequirement', label: 'Evidence requirement', type: 'textarea' }, { key: 'kpiTarget', label: 'KPI target' }
  ];
}

export function deliverableFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'name', label: 'Name', required: true, wide: true }, { key: 'description', label: 'Description', type: 'textarea' },
    { key: 'dueDate', label: 'Due date', type: 'date', required: true }, { key: 'payableAmount', label: 'Payable amount (R)', type: 'number', min: 0 },
    { key: 'acceptanceCriteria', label: 'Acceptance criteria', type: 'textarea' }, { key: 'evidenceRequired', label: 'Evidence required', type: 'checkbox' }
  ];
}

export function paymentScheduleFields(): Field[] {
  return [
    { key: 'description', label: 'Description', required: true, wide: true },
    { key: 'amount', label: 'Amount (R)', type: 'number', required: true, min: 0 }, { key: 'plannedDate', label: 'Planned date', type: 'date', required: true }
  ];
}

export function variationFields(): Field[] {
  return [
    { key: 'type', label: 'Type', type: 'select', required: true, options: [{ value: 'Scope', label: 'Scope' }, { value: 'Value', label: 'Value' },
      { value: 'Time', label: 'Time' }, { value: 'Combined', label: 'Combined' }] },
    { key: 'isExtension', label: 'Extension (time)', type: 'checkbox' },
    { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
    { key: 'reason', label: 'Reason', type: 'textarea', required: true, wide: true },
    { key: 'amount', label: 'Value change (R)', type: 'number' }, { key: 'days', label: 'Time change (days)', type: 'number' },
    { key: 'revisedEndDate', label: 'Revised end date', type: 'date' }
  ];
}

export function reviewFields(): Field[] {
  return [
    { key: 'period', label: 'Period', required: true, hint: 'e.g. 2026 Q2' }, { key: 'reviewDate', label: 'Review date', type: 'date', required: true },
    { key: 'qualityScore', label: 'Quality score (0-100)', type: 'number', required: true, min: 0, max: 100 },
    { key: 'timelinessScore', label: 'Timeliness score (0-100)', type: 'number', required: true, min: 0, max: 100 },
    { key: 'complianceScore', label: 'Compliance score (0-100)', type: 'number', required: true, min: 0, max: 100 },
    { key: 'comments', label: 'Comments', type: 'textarea', wide: true }
  ];
}

export function breachFields(): Field[] {
  return [
    { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
    { key: 'severity', label: 'Severity', type: 'select', required: true, options: [{ value: 'Low', label: 'Low' }, { value: 'Medium', label: 'Medium' },
      { value: 'High', label: 'High' }, { value: 'Critical', label: 'Critical' }] },
    { key: 'identifiedOn', label: 'Identified on', type: 'date', required: true }, { key: 'noticeDate', label: 'Notice date', type: 'date' },
    { key: 'noticeReference', label: 'Notice reference' }, { key: 'remedy', label: 'Remedy', type: 'textarea' },
    { key: 'remedyDueDate', label: 'Remedy due date', type: 'date' }, { key: 'penaltyAmount', label: 'Penalty amount (R)', type: 'number', min: 0 }
  ];
}
