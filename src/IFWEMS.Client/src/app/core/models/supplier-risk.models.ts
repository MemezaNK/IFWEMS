export interface SupplierRiskProfileDto {
  supplierId: string;
  supplierCode: string;
  name: string;
  deviationCount: number;
  caseCount: number;
  contractCount: number;
  totalContractValue: number;
  concentrationPercentage: number;
  riskRating: string;
}
