import { Field } from '../../shared/form-dialog.component';

export function supplierFields(): Field[] {
  return [
    { key: 'legalName', label: 'Legal name', required: true, wide: true, maxLength: 200 },
    { key: 'tradingName', label: 'Trading name' },
    { key: 'registrationNumber', label: 'Registration number' }, { key: 'csdNumber', label: 'CSD number' },
    { key: 'taxNumber', label: 'Tax number' }, { key: 'vatNumber', label: 'VAT number' },
    { key: 'bbbeeLevel', label: 'B-BBEE level', type: 'number', min: 1, max: 8 },
    { key: 'email', label: 'E-mail', type: 'email' }, { key: 'phone', label: 'Phone' },
    { key: 'address', label: 'Address', type: 'textarea', wide: true },
    { key: 'province', label: 'Province' }, { key: 'isImplementingPartner', label: 'Implementing partner', type: 'checkbox' },
    { key: 'csdVerified', label: 'CSD verified', type: 'checkbox' }, { key: 'taxClearanceExpiry', label: 'Tax clearance expiry', type: 'date' },
    { key: 'erpVendorCode', label: 'ERP vendor code' }
  ];
}
