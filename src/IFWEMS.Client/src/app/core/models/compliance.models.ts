export interface TransactionCheckRequest {
  transactionReference: string;
  orgUnitId: string;
  supplierId?: string | null;
  contractId?: string | null;
  amount: number;
  invoiceReference?: string | null;
  purchaseOrderDateUtc?: string | null;
  commodityCode?: string | null;
}

export interface TransactionCheckResult {
  riskScore: number;
  riskRating: 'GREEN' | 'AMBER' | 'RED';
  failedRuleCodes: string[];
  recommendedAction: 'PASS' | 'REVIEW' | 'BLOCK';
}

export interface EmergencyOverrideRequest {
  transactionReference: string;
  orgUnitId: string;
  supplierId?: string | null;
  amount: number;
  emergencyCategory: string;
  reason: string;
  evidenceDocumentId: string;
  commodityCode?: string | null;
}
