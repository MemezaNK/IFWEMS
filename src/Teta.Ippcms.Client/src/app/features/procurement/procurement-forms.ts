import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

export const METHOD_CODES = ['PETTY_CASH', 'QUOTATIONS', 'RFQ', 'OPEN_TENDER'];

export function planItemFields(refs: ReferenceService, editing = false): Field[] {
  return [
    { key: 'financialYear', label: 'Financial year', required: true, hint: 'e.g. 2026/27', maxLength: 10 },
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'description', label: 'Description', type: 'textarea', required: true },
    { key: 'estimatedValue', label: 'Estimated value (R)', type: 'number', required: true, min: 0 },
    { key: 'plannedMethod', label: 'Planned method', type: 'select', options: refs.enumOptions(METHOD_CODES) },
    { key: 'plannedRequisitionDate', label: 'Planned requisition date', type: 'date' },
    { key: 'plannedAdvertDate', label: 'Planned advert date', type: 'date' },
    { key: 'plannedAwardDate', label: 'Planned award date', type: 'date' },
    { key: 'orgUnit', label: 'Organisational unit', type: 'select', options: refs.category('OrgUnit') },
    ...(editing ? [{ key: 'status', label: 'Status', type: 'select' as const, options: refs.enumOptions(['Planned', 'InProgress', 'Awarded', 'Deferred', 'Cancelled']) }] : []),
    { key: 'varianceReason', label: 'Variance reason', type: 'textarea' }
  ];
}

export function methodRuleFields(): Field[] {
  return [
    { key: 'methodCode', label: 'Method code', required: true, maxLength: 30 },
    { key: 'name', label: 'Name', required: true, wide: true },
    { key: 'minValue', label: 'Minimum value (R)', type: 'number', required: true, min: 0 },
    { key: 'maxValue', label: 'Maximum value (R, blank = no limit)', type: 'number', min: 0 },
    { key: 'effectiveFrom', label: 'Effective from', type: 'date', required: true },
    { key: 'effectiveTo', label: 'Effective to', type: 'date' },
    { key: 'minimumQuotations', label: 'Minimum quotations', type: 'number', required: true, min: 0 },
    { key: 'minimumAdvertDays', label: 'Minimum advert days', type: 'number', required: true, min: 0 },
    { key: 'requiresPublication', label: 'Requires public advertisement', type: 'checkbox' },
    { key: 'approvalAuthority', label: 'Approval authority', required: true },
    { key: 'policyReference', label: 'Policy reference' }
  ];
}

export function requisitionFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'title', label: 'Title', required: true, wide: true, maxLength: 200 },
    { key: 'description', label: 'Description', type: 'textarea', required: true },
    { key: 'estimatedValue', label: 'Estimated value (R)', type: 'number', required: true, min: 0 },
    { key: 'requiredByDate', label: 'Required by', type: 'date' },
    { key: 'selectedMethod', label: 'Selected method (optional override)', type: 'select', options: refs.enumOptions(METHOD_CODES) },
    { key: 'methodJustification', label: 'Method justification', type: 'textarea' }
  ];
}

export function exceptionFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project (optional)', type: 'select', options: refs.projects(), wide: true },
    { key: 'type', label: 'Type', type: 'select', required: true,
      options: refs.enumOptions(['Deviation', 'Emergency', 'SingleSource', 'BudgetException', 'Other']) },
    { key: 'motivation', label: 'Motivation', type: 'textarea', required: true },
    { key: 'authority', label: 'Approval authority', required: true },
    { key: 'value', label: 'Value (R)', type: 'number', min: 0 }
  ];
}

export function specificationFields(): Field[] {
  return [
    { key: 'title', label: 'Title', required: true, wide: true },
    { key: 'content', label: 'Content / terms of reference', type: 'textarea', required: true, wide: true }
  ];
}

export function committeeFields(): Field[] {
  return [
    { key: 'type', label: 'Committee type', type: 'select', required: true,
      options: [{ value: 'BidSpecification', label: 'Bid specification' }, { value: 'BidEvaluation', label: 'Bid evaluation' },
        { value: 'BidAdjudication', label: 'Bid adjudication' }] },
    { key: 'name', label: 'Name', required: true, wide: true },
    { key: 'quorum', label: 'Quorum', type: 'number', required: true, min: 1 }
  ];
}

export function memberFields(refs: ReferenceService): Field[] {
  return [
    { key: 'userId', label: 'User', type: 'select', required: true, options: refs.users(), wide: true },
    { key: 'role', label: 'Role', type: 'select', required: true,
      options: [{ value: 'Chairperson', label: 'Chairperson' }, { value: 'Member', label: 'Member' },
        { value: 'Secretariat', label: 'Secretariat' }, { value: 'Observer', label: 'Observer' }] },
    { key: 'accessFrom', label: 'Access from', type: 'date', required: true },
    { key: 'accessTo', label: 'Access to', type: 'date', required: true }
  ];
}

export function meetingFields(): Field[] {
  return [
    { key: 'meetingAtUtc', label: 'Meeting date/time', type: 'datetime', required: true },
    { key: 'agenda', label: 'Agenda', type: 'textarea', required: true, wide: true },
    { key: 'minutes', label: 'Minutes', type: 'textarea', wide: true },
    { key: 'attendeesCount', label: 'Attendees', type: 'number', required: true, min: 0 },
    { key: 'resolutions', label: 'Resolutions', type: 'textarea' }
  ];
}

export function publicationFields(): Field[] {
  return [
    { key: 'channel', label: 'Channel', required: true, hint: 'e.g. eTender portal, newspaper' },
    { key: 'reference', label: 'Advert reference', required: true },
    { key: 'publishedOn', label: 'Published on', type: 'date', required: true },
    { key: 'closingDateUtc', label: 'Closing date/time', type: 'datetime', required: true },
    { key: 'url', label: 'URL', wide: true },
    { key: 'briefingSession', label: 'Briefing session details', type: 'textarea' }
  ];
}

export function bidFields(refs: ReferenceService): Field[] {
  return [
    { key: 'supplierId', label: 'Supplier', type: 'select', required: true, options: refs.suppliers(), wide: true },
    { key: 'bidReference', label: 'Bid reference', required: true },
    { key: 'bidAmount', label: 'Bid amount (R)', type: 'number', required: true, min: 0 },
    { key: 'specificGoalsClaimed', label: 'Specific goals claimed (points)', type: 'number', min: 0 },
    { key: 'submissionNotes', label: 'Submission notes', type: 'textarea' }
  ];
}

export function criterionFields(): Field[] {
  return [
    { key: 'stage', label: 'Stage', type: 'select', required: true,
      options: [{ value: 'Compliance', label: 'Compliance' }, { value: 'Technical', label: 'Technical' }] },
    { key: 'name', label: 'Name', required: true, wide: true },
    { key: 'description', label: 'Description', type: 'textarea' },
    { key: 'weight', label: 'Weight', type: 'number', required: true, min: 0 },
    { key: 'maxScore', label: 'Maximum score', type: 'number', required: true, min: 0 },
    { key: 'isMandatory', label: 'Mandatory (pass/fail)', type: 'checkbox' },
    { key: 'sortOrder', label: 'Sort order', type: 'number', min: 0 }
  ];
}

export function communicationFields(refs: ReferenceService): Field[] {
  return [
    { key: 'supplierId', label: 'Supplier (optional)', type: 'select', options: refs.suppliers(), wide: true },
    { key: 'type', label: 'Type', type: 'select', required: true,
      options: refs.enumOptions(['AwardNotice', 'RegretLetter', 'Debriefing', 'Objection', 'ObjectionOutcome', 'Clarification']) },
    { key: 'date', label: 'Date', type: 'date', required: true },
    { key: 'subject', label: 'Subject', required: true, wide: true },
    { key: 'details', label: 'Details', type: 'textarea', required: true, wide: true },
    { key: 'outcome', label: 'Outcome' }
  ];
}
