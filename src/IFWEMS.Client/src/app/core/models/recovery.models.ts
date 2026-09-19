export type RecoveryStatus = 'Recovered' | 'WrittenOff';

export interface RecoveryDto {
  id: string;
  caseId: string;
  amount: number;
  status: RecoveryStatus;
  reference: string | null;
  createdAtUtc: string;
}

export interface CaptureRecoveryRequest {
  amount: number;
  reference: string | null;
}

export interface ApproveWriteOffRequest {
  reason: string;
}
