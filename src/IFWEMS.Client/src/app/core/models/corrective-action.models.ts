export type CorrectiveActionStatus = 'Pending' | 'Verified';

export interface CorrectiveActionDto {
  id: string;
  caseId: string;
  controlId: string;
  description: string;
  status: CorrectiveActionStatus;
  evidenceDocumentId: string | null;
  verifiedBy: string | null;
}

export interface CreateCorrectiveActionRequest {
  controlId: string;
  description: string;
}

export interface VerifyCorrectiveActionRequest {
  evidenceDocumentId: string;
}
