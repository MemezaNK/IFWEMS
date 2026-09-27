import { AUTHORITY_TYPES, ROLE_CODES } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

const opts = (values: string[]) => values.map(v => ({ value: v, label: v.replace(/([a-z])([A-Z])/g, '$1 $2') }));

export function delegationFields(refs: ReferenceService): Field[] {
  return [
    { key: 'authorityType', label: 'Authority type', type: 'select', required: true, options: opts(AUTHORITY_TYPES) },
    { key: 'roleCode', label: 'Role (or leave blank and pick a user)', type: 'select', options: opts(ROLE_CODES) },
    { key: 'userId', label: 'User (or leave blank and pick a role)', type: 'select', options: refs.users() },
    { key: 'maxAmount', label: 'Maximum amount (R)', type: 'number', required: true, min: 0 },
    { key: 'effectiveFrom', label: 'Effective from', type: 'date', required: true }, { key: 'effectiveTo', label: 'Effective to (optional)', type: 'date' },
    { key: 'isActive', label: 'Active', type: 'checkbox' }, { key: 'policyReference', label: 'Policy reference', wide: true }
  ];
}

export function substitutionFields(refs: ReferenceService): Field[] {
  return [
    { key: 'principalUserId', label: 'Acting for (leave blank for yourself)', type: 'select', options: refs.users() },
    { key: 'substituteUserId', label: 'Substitute', type: 'select', required: true, options: refs.users() },
    { key: 'fromUtc', label: 'From', type: 'datetime', required: true }, { key: 'toUtc', label: 'To', type: 'datetime', required: true },
    { key: 'reason', label: 'Reason', type: 'textarea', required: true, wide: true }
  ];
}

export function sodRuleFields(): Field[] {
  return [
    { key: 'code', label: 'Code', required: true }, { key: 'description', label: 'Description', required: true, wide: true },
    { key: 'entityType', label: 'Entity type', required: true, hint: 'e.g. Procurement, Contract, Invoice' },
    { key: 'firstAction', label: 'First action', required: true }, { key: 'secondAction', label: 'Second action (may not be by the same user)', required: true },
    { key: 'mode', label: 'Mode', type: 'select', required: true, options: opts(['Block', 'Escalate']) },
    { key: 'isActive', label: 'Active', type: 'checkbox' }
  ];
}

export function referenceItemFields(): Field[] {
  return [
    { key: 'category', label: 'Category', required: true }, { key: 'code', label: 'Code', required: true },
    { key: 'name', label: 'Name', required: true, wide: true }, { key: 'description', label: 'Description', type: 'textarea' },
    { key: 'sortOrder', label: 'Sort order', type: 'number' }, { key: 'isActive', label: 'Active', type: 'checkbox' }
  ];
}

export function holidayFields(): Field[] {
  return [{ key: 'date', label: 'Date', type: 'date', required: true }, { key: 'name', label: 'Name', required: true, wide: true }];
}

export function settingFields(): Field[] {
  return [{ key: 'value', label: 'Value', required: true, wide: true }];
}

export function templateFields(): Field[] {
  return [
    { key: 'subject', label: 'Subject', required: true, wide: true }, { key: 'body', label: 'Body', type: 'textarea', required: true, wide: true,
      hint: 'Placeholders such as {{reference}} are substituted when the notification is sent.' },
    { key: 'sendEmail', label: 'Send by email', type: 'checkbox' }, { key: 'isActive', label: 'Active', type: 'checkbox' }
  ];
}

export function retentionFields(): Field[] {
  return [
    { key: 'recordClass', label: 'Record class', required: true, wide: true }, { key: 'retentionYears', label: 'Retention (years)', type: 'number', required: true, min: 0 },
    { key: 'disposalAction', label: 'Disposal action', type: 'select', required: true, options: opts(['Archive', 'Destroy', 'Review']) },
    { key: 'legalReference', label: 'Legal reference', wide: true }, { key: 'isActive', label: 'Active', type: 'checkbox' }
  ];
}

export function stepFields(): Field[] {
  return [
    { key: 'code', label: 'Step code', required: true }, { key: 'name', label: 'Step name', required: true, wide: true },
    { key: 'requiredRole', label: 'Required role', type: 'select', required: true, options: opts(ROLE_CODES) },
    { key: 'authorityType', label: 'Authority type (for delegated limits)', type: 'select', options: opts(AUTHORITY_TYPES) },
    { key: 'minimumValue', label: 'Applies from value (R, optional)', type: 'number', min: 0 },
    { key: 'maximumValue', label: 'Applies up to value (R, optional)', type: 'number', min: 0 },
    { key: 'slaHours', label: 'SLA (hours)', type: 'number', required: true, min: 1 },
    { key: 'escalationRole', label: 'Escalation role (optional)', type: 'select', options: opts(ROLE_CODES) }
  ];
}
