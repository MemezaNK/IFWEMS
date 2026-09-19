export type InvestigationStatus = 'PendingApproval' | 'Approved' | 'Rejected';

export interface InvestigationDto {
  id: string;
  caseId: string;
  investigatorUserId: string;
  status: InvestigationStatus;
  findings: string | null;
  recommendation: string | null;
  createdAtUtc: string;
}

export interface CreateInvestigationRequest {
  findings: string | null;
  recommendation: string | null;
}

export interface ApproveInvestigationRequest {
  approve: boolean;
  comments: string | null;
}
