export interface TransactionDto {
  id: string;
  transactionReference: string;
  orgUnitId: string;
  orgUnitName: string;
  supplierId: string | null;
  supplierName: string | null;
  amount: number;
  riskScore: number;
  riskRating: 'GREEN' | 'AMBER' | 'RED';
  recommendedAction: string;
  failedRuleCodes: string[];
  isEmergencyOverride: boolean;
  overrideReason: string | null;
  createdAtUtc: string;
}

export interface TransactionPageDto {
  items: TransactionDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface TransactionQuery {
  riskRating?: string;
  orgUnitId?: string;
  supplierId?: string;
  emergencyOverrideOnly?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}
