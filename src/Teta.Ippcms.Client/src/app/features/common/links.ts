/** Maps a workflow/audit entity type to the page that shows it. */
export function entityLink(entityType: string, entityId: string, projectId?: string | null): string {
  switch (entityType) {
    case 'BusinessCase': return projectId ? `/projects/${projectId}` : '/projects';
    case 'ProjectClosure': return projectId ? `/projects/${projectId}` : '/projects';
    case 'Project': return `/projects/${entityId}`;
    case 'Procurement': return `/procurement/${entityId}`;
    case 'Requisition': return '/procurement/requisitions';
    case 'ProcurementException': return '/procurement/exceptions';
    case 'ContractVariation': return '/contracts';
    case 'Contract': return `/contracts/${entityId}`;
    case 'Invoice': return `/finance/invoices/${entityId}`;
    case 'ChangeRequest': return projectId ? `/projects/${projectId}` : '/change-requests';
    case 'StrategicPlan': return `/strategy/plans/${entityId}`;
    case 'AppTarget': return '/strategy/plans';
    case 'Risk': return `/assurance/risks/${entityId}`;
    default: return '/';
  }
}
