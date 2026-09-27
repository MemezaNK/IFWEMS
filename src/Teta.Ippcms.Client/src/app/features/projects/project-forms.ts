import { ReferenceService } from '../../core/reference.service';
import { Field } from '../../shared/form-dialog.component';

export function projectFields(refs: ReferenceService): Field[] {
  return [
    { key: 'name', label: 'Project name', required: true, wide: true, maxLength: 200 },
    { key: 'description', label: 'Description', type: 'textarea' },
    { key: 'programmeId', label: 'Programme', type: 'select', required: true, options: refs.programmes() },
    { key: 'projectType', label: 'Project type', type: 'select', options: refs.category('ProjectType') },
    { key: 'managerUserId', label: 'Project manager', type: 'select', options: refs.users() },
    { key: 'sponsorUserId', label: 'Sponsor', type: 'select', options: refs.users() },
    { key: 'businessOwner', label: 'Business owner' },
    { key: 'orgUnit', label: 'Organisational unit', type: 'select', options: refs.category('OrgUnit') },
    { key: 'province', label: 'Province', type: 'select', options: refs.category('Province') },
    { key: 'district', label: 'District' },
    { key: 'municipality', label: 'Municipality' },
    { key: 'plannedStart', label: 'Planned start', type: 'date' },
    { key: 'plannedEnd', label: 'Planned end', type: 'date' }
  ];
}

export function businessCaseFields(): Field[] {
  return [
    { key: 'problem', label: 'Problem statement', type: 'textarea', required: true },
    { key: 'objectives', label: 'Objectives', type: 'textarea', required: true },
    { key: 'options', label: 'Options considered', type: 'textarea', required: true },
    { key: 'scope', label: 'Scope', type: 'textarea', required: true },
    { key: 'benefits', label: 'Benefits', type: 'textarea', required: true },
    { key: 'estimatedCost', label: 'Estimated cost (R)', type: 'number', required: true, min: 0 },
    { key: 'risks', label: 'Key risks', type: 'textarea', required: true },
    { key: 'deliveryModel', label: 'Delivery model', type: 'textarea', required: true },
    { key: 'expectedBenefitMeasure', label: 'Benefit measure' },
    { key: 'expectedBenefitValue', label: 'Expected benefit value', type: 'number' }
  ];
}
