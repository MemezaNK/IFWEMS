export interface SlaPolicyDto {
  id: string;
  caseType: string;
  status: string;
  maxDurationHours: number;
  isActive: boolean;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
}

export interface CreateSlaPolicyRequest {
  caseType: string;
  status: string;
  maxDurationHours: number;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
}

export interface OverdueCaseDto {
  caseId: string;
  caseNumber: string;
  caseType: string;
  status: string;
  enteredStatusAtUtc: string;
  hoursInStatus: number;
  slaMaxHours: number;
  hoursOverdue: number;
}
