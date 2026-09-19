export interface DocumentDto {
  id: string;
  fileName: string;
  documentType: string;
  confidentialityClassification: string;
  integrityHash: string;
  version: number;
  uploadedBy: string;
  caseId: string | null;
  createdAtUtc: string;
}
