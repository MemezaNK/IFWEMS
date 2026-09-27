import { ROLE_CODES } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

const opts = (values: string[]) => values.map(v => ({ value: v, label: v.replace(/([a-z])([A-Z])/g, '$1 $2') }));
export const SCOPE_TYPES = ['Global', 'Portfolio', 'Programme', 'Project'];

export function createUserFields(): Field[] {
  return [
    { key: 'username', label: 'Username', required: true }, { key: 'email', label: 'Email', type: 'email', required: true },
    { key: 'displayName', label: 'Display name', required: true, wide: true },
    { key: 'initialPassword', label: 'Initial password', type: 'password', required: true, hint: 'The user must change this on first sign-in.' }
  ];
}

export function assignmentFields(refs: ReferenceService, userId?: string): Field[] {
  const fields: Field[] = [];
  if (!userId) fields.push({ key: 'userId', label: 'User', type: 'select', required: true, options: refs.users(), wide: true });
  fields.push(
    { key: 'roleCode', label: 'Role', type: 'select', required: true, options: opts(ROLE_CODES) },
    { key: 'scopeType', label: 'Scope', type: 'select', required: true, options: opts(SCOPE_TYPES) },
    { key: 'scopeId', label: 'Scope record ID (portfolio/programme/project — leave blank for Global)', wide: true },
    { key: 'effectiveFrom', label: 'Effective from', type: 'date', required: true }, { key: 'effectiveTo', label: 'Effective to (optional)', type: 'date' },
    { key: 'reason', label: 'Reason', type: 'textarea', required: true, wide: true }
  );
  return fields;
}
