import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

export function mePlanFields(refs: ReferenceService): Field[] {
  return [
    { key: 'description', label: 'Monitoring approach', type: 'textarea', required: true, wide: true },
    { key: 'frequency', label: 'Frequency', required: true, hint: 'e.g. Quarterly' },
    { key: 'methods', label: 'Methods', required: true, hint: 'e.g. Site visits, desktop review, beneficiary interviews' },
    { key: 'indicators', label: 'Key indicators monitored', type: 'textarea' },
    { key: 'responsibleOfficerUserId', label: 'Responsible officer', type: 'select', options: refs.users() },
    { key: 'responsibleOfficerName', label: 'Responsible officer name (if not a system user)' },
    { key: 'nextVisitDue', label: 'Next visit due', type: 'date' }
  ];
}

export function scheduleVisitFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'type', label: 'Type', type: 'select', required: true, options: [{ value: 'Desktop', label: 'Desktop review' }, { value: 'Site', label: 'Site visit' }] },
    { key: 'scheduledDate', label: 'Scheduled date', type: 'date', required: true },
    { key: 'officials', label: 'Officials attending' }, { key: 'location', label: 'Location' },
    { key: 'providerSupplierId', label: 'Implementing partner / provider (optional)', type: 'select', options: refs.suppliers(), wide: true }
  ];
}

export function findingFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
    { key: 'severity', label: 'Severity', type: 'select', required: true, options: [{ value: 'Low', label: 'Low' }, { value: 'Medium', label: 'Medium' },
      { value: 'High', label: 'High' }, { value: 'Critical', label: 'Critical' }] },
    { key: 'rootCause', label: 'Root cause', type: 'textarea' }
  ];
}

export function actionFields(refs: ReferenceService): Field[] {
  return [
    { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name (if not a system user)' },
    { key: 'dueDate', label: 'Due date', type: 'date', required: true }
  ];
}

export function beneficiaryFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'identifier', label: 'ID / passport number', required: true }, { key: 'identifierType', label: 'Identifier type', required: true, hint: 'e.g. RSA ID, Passport' },
    { key: 'firstName', label: 'First name', required: true }, { key: 'lastName', label: 'Last name', required: true },
    { key: 'gender', label: 'Gender' }, { key: 'birthYear', label: 'Birth year', type: 'number', min: 1900, max: 2026 },
    { key: 'province', label: 'Province' }, { key: 'district', label: 'District' },
    { key: 'intervention', label: 'Intervention', required: true, wide: true },
    { key: 'providerSupplierId', label: 'Implementing partner (optional)', type: 'select', options: refs.suppliers(), wide: true },
    { key: 'fundingSource', label: 'Funding source' }, { key: 'consentObtained', label: 'Consent obtained', type: 'checkbox', required: true }
  ];
}
