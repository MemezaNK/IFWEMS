export interface ContractDto {
  id: string;
  contractNumber: string;
  title: string;
  supplierId: string;
  originalValue: number;
  currentValue: number;
  utilisedValue: number;
  utilisationPercentage: number;
  startDateUtc: string;
  expiryDateUtc: string;
  daysToExpiry: number;
  alertLevel: string | null;
}
