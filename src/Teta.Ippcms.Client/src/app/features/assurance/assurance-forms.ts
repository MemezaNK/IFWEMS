import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

export const LIKELIHOOD_IMPACT = [1, 2, 3, 4, 5].map(v => ({ value: v, label: String(v) }));
export const EFFECTIVENESS = ['Effective', 'PartiallyEffective', 'NotEffective', 'NotAssessed'].map(v => ({ value: v, label: v }));

export function riskFields(refs: ReferenceService, editing = false): Field[] {
  return [
    { key: 'parentType', label: 'Parent type', type: 'select', required: true,
      options: [{ value: 'Project', label: 'Project' }, { value: 'Contract', label: 'Contract' }, { value: 'Programme', label: 'Programme' }] },
    { key: 'parentId', label: 'Parent (project) ID', required: true, wide: true, hint: 'The project, contract or programme this risk belongs to' },
    { key: 'title', label: 'Title', required: true, wide: true },
    { key: 'cause', label: 'Cause', type: 'textarea', required: true }, { key: 'event', label: 'Event / risk', type: 'textarea', required: true },
    { key: 'consequence', label: 'Consequence', type: 'textarea', required: true }, { key: 'category', label: 'Category' },
    { key: 'inherentLikelihood', label: 'Inherent likelihood (1-5)', type: 'select', required: true, options: LIKELIHOOD_IMPACT },
    { key: 'inherentImpact', label: 'Inherent impact (1-5)', type: 'select', required: true, options: LIKELIHOOD_IMPACT },
    { key: 'residualLikelihood', label: 'Residual likelihood (1-5)', type: 'select', required: true, options: LIKELIHOOD_IMPACT },
    { key: 'residualImpact', label: 'Residual impact (1-5)', type: 'select', required: true, options: LIKELIHOOD_IMPACT },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name (if not a system user)' },
    { key: 'reviewDate', label: 'Next review date', type: 'date', required: true },
    ...(editing ? [{ key: 'status', label: 'Status', type: 'select' as const, options: [{ value: 'Open', label: 'Open' }, { value: 'Treating', label: 'Treating' },
      { value: 'Accepted', label: 'Accepted' }, { value: 'Closed', label: 'Closed' }] }] : [])
  ];
}

export function controlFields(refs: ReferenceService): Field[] {
  return [
    { key: 'name', label: 'Control name', required: true, wide: true }, { key: 'description', label: 'Description', type: 'textarea' },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name (if not a system user)' }
  ];
}

export function treatmentFields(refs: ReferenceService): Field[] {
  return [
    { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name (if not a system user)' },
    { key: 'dueDate', label: 'Due date', type: 'date', required: true }
  ];
}

export function obligationFields(refs: ReferenceService): Field[] {
  return [
    { key: 'code', label: 'Code', required: true }, { key: 'title', label: 'Title', required: true, wide: true },
    { key: 'source', label: 'Source', required: true, hint: 'e.g. PFMA, Treasury Regulations, Municipal By-law' },
    { key: 'description', label: 'Description', type: 'textarea' }, { key: 'frequency', label: 'Attestation frequency', required: true, hint: 'e.g. Quarterly, Annual' },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: refs.users() }, { key: 'ownerName', label: 'Owner name (if not a system user)' },
    { key: 'isActive', label: 'Active', type: 'checkbox' }
  ];
}

export function auditFindingFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project (optional)', type: 'select', options: refs.projects(), wide: true },
    { key: 'process', label: 'Process / area', required: true },
    { key: 'source', label: 'Source', type: 'select', required: true, options: [{ value: 'InternalAudit', label: 'Internal audit' },
      { value: 'ExternalAudit', label: 'External audit' }, { value: 'RiskReview', label: 'Risk review' }, { value: 'Other', label: 'Other' }] },
    { key: 'auditReference', label: 'Audit reference' }, { key: 'title', label: 'Title', required: true, wide: true },
    { key: 'description', label: 'Description', type: 'textarea', required: true, wide: true },
    { key: 'recommendation', label: 'Recommendation', type: 'textarea' },
    { key: 'rating', label: 'Rating', type: 'select', required: true, options: [{ value: 'Low', label: 'Low' }, { value: 'Medium', label: 'Medium' },
      { value: 'High', label: 'High' }, { value: 'Critical', label: 'Critical' }] },
    { key: 'managementResponse', label: 'Management response', type: 'textarea' },
    { key: 'actionOwnerUserId', label: 'Action owner', type: 'select', options: refs.users() }, { key: 'actionOwnerName', label: 'Action owner name' },
    { key: 'dueDate', label: 'Due date', type: 'date', required: true }
  ];
}

export function coverageFields(refs: ReferenceService): Field[] {
  return [
    { key: 'area', label: 'Area', required: true, wide: true }, { key: 'riskId', label: 'Related risk ID (optional)' },
    { key: 'line', label: 'Line of assurance', type: 'select', required: true, options: [{ value: 'Management', label: '1st line: Management' },
      { value: 'RiskAndCompliance', label: '2nd line: Risk & compliance' }, { value: 'InternalAudit', label: '3rd line: Internal audit' },
      { value: 'ExternalAssurance', label: 'External assurance' }] },
    { key: 'period', label: 'Period', required: true, hint: 'e.g. 2026 Q2' }, { key: 'covered', label: 'Covered', type: 'checkbox' },
    { key: 'provider', label: 'Provider' }, { key: 'rating', label: 'Rating' }, { key: 'comments', label: 'Comments', type: 'textarea' }
  ];
}
