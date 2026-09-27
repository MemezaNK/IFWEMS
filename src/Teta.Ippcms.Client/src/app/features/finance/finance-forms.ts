import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

export function commitmentFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'contractId', label: 'Contract (optional)', wide: true, hint: 'Contract ID, if this commitment relates to a specific contract' },
    { key: 'poReference', label: 'PO reference', required: true }, { key: 'amount', label: 'Amount (R)', type: 'number', required: true, min: 0 },
    { key: 'commitmentDate', label: 'Commitment date', type: 'date', required: true }, { key: 'erpReference', label: 'ERP reference' }
  ];
}

export function registerInvoiceFields(): Field[] {
  return [
    { key: 'contractId', label: 'Contract ID', required: true, wide: true, hint: 'From the contract register / detail page' },
    { key: 'deliverableId', label: 'Deliverable ID (optional)', wide: true },
    { key: 'supplierInvoiceNumber', label: 'Supplier invoice number', required: true },
    { key: 'poReference', label: 'PO reference' },
    { key: 'invoiceDate', label: 'Invoice date', type: 'date', required: true }, { key: 'receivedDate', label: 'Received date', type: 'date' },
    { key: 'amount', label: 'Amount (R, excl. VAT)', type: 'number', required: true, min: 0 }, { key: 'vatAmount', label: 'VAT amount (R)', type: 'number', min: 0 }
  ];
}

export function accrualFields(refs: ReferenceService): Field[] {
  return [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: refs.projects(), wide: true },
    { key: 'period', label: 'Period', required: true, hint: 'e.g. 2026 Q2' }, { key: 'amount', label: 'Amount (R)', type: 'number', required: true, min: 0 },
    { key: 'description', label: 'Description', type: 'textarea' }
  ];
}

export function forecastFields(): Field[] {
  return [
    { key: 'estimateAtCompletion', label: 'Estimate at completion (R)', type: 'number', required: true, min: 0 },
    { key: 'commentary', label: 'Commentary', type: 'textarea', required: true, wide: true }
  ];
}
