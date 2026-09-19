export type CaseType = 'IrregularExpenditure' | 'FruitlessWasteful' | 'UnauthorisedExpenditure' | 'PotentialNonCompliance';
export type CaseStatus =
  | 'Draft'
  | 'UnderAssessment'
  | 'UnderInvestigation'
  | 'Determined'
  | 'RecoveryInProgress'
  | 'ConsequenceManagement'
  | 'CorrectiveActionPending'
  | 'Closed';

export interface CaseDto {
  id: string;
  caseNumber: string;
  caseType: CaseType;
  status: CaseStatus;
  orgUnitId: string;
  title: string;
  description: string | null;
  amountInvolved: number | null;
  recoverableAmount: number;
  recoveredAmount: number;
  createdAtUtc: string;
}

export interface CreateCaseRequest {
  caseType: CaseType;
  orgUnitId: string;
  title: string;
  description: string | null;
  amountInvolved: number | null;
  supplierId?: string | null;
  contractId?: string | null;
}

export interface ChangeCaseStatusRequest {
  newStatus: CaseStatus;
  reason: string;
}

