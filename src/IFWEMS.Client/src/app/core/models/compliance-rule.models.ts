export interface ComplianceRuleDto {
  id: string;
  code: string;
  name: string;
  description: string;
  version: number;
  isActive: boolean;
  isApproved: boolean;
  parametersJson: string;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
}

export interface CreateComplianceRuleRequest {
  code: string;
  name: string;
  description: string;
  parametersJson: string;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
}
