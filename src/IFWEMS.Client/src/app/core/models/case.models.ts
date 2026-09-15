export type CaseType = 'IrregularExpenditure' | 'FruitlessAndWastefulExpenditure' | 'UnauthorisedExpenditure' | 'PotentialNonCompliance';
export type CaseStatus =
  | 'Draft'
  | 'UnderAssessment'
  | 'UnderInvestigation'
  | 'Determined'
  | 'UnderRecovery'
  | 'AwaitingCorrectiveAction'
  | 'Closed';

export interface CaseDto {
  id: string;
  caseNumber: string;
  caseType: CaseType;
  status: CaseStatus;
  orgUnitId: string;
  title: string;
  description: string;
  recoverableAmount: number;
  recoveredAmount: number;
  createdAtUtc: string;
}

export interface CreateCaseRequest {
  caseType: CaseType;
  orgUnitId: string;
  title: string;
  description: string;
  recoverableAmount: number;
  supplierId?: string | null;
  contractId?: string | null;
}

export interface ChangeCaseStatusRequest {
  newStatus: CaseStatus;
  reason: string;
}
