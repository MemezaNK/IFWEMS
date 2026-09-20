import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CaseService } from '../../core/cases/case.service';
import { CaseDto, CaseStatus } from '../../core/models/case.models';
import { InvestigationsService } from '../../core/investigations/investigations.service';
import { InvestigationDto } from '../../core/models/investigation.models';
import { RecoveriesService } from '../../core/recoveries/recoveries.service';
import { RecoveryDto } from '../../core/models/recovery.models';
import { CorrectiveActionsService } from '../../core/corrective-actions/corrective-actions.service';
import { CorrectiveActionDto } from '../../core/models/corrective-action.models';
import { ControlsService } from '../../core/controls/controls.service';
import { ControlDto } from '../../core/models/control.models';
import { DocumentsService } from '../../core/documents/documents.service';
import { DocumentDto } from '../../core/models/document.models';
import { AuthService } from '../../core/auth/auth.service';
import { EnumLabelPipe } from '../../core/shared/enum-label.pipe';

@Component({
  selector: 'app-case-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    EnumLabelPipe
  ],
  templateUrl: './case-detail.component.html',
  styleUrl: './case-detail.component.scss'
})
export class CaseDetailComponent implements OnInit {
  readonly caseId: string;
  readonly caseDetail = signal<CaseDto | null>(null);
  readonly investigations = signal<InvestigationDto[]>([]);
  readonly recoveries = signal<RecoveryDto[]>([]);
  readonly correctiveActions = signal<CorrectiveActionDto[]>([]);
  readonly controls = signal<ControlDto[]>([]);
  readonly documents = signal<DocumentDto[]>([]);
  readonly errorMessage = signal<string | null>(null);

  readonly statuses: CaseStatus[] = [
    'Draft',
    'UnderAssessment',
    'UnderInvestigation',
    'Determined',
    'RecoveryInProgress',
    'ConsequenceManagement',
    'CorrectiveActionPending',
    'Closed'
  ];

  newStatus: CaseStatus | '' = '';
  statusReason = '';

  newInvestigation = { findings: '', recommendation: '' };
  newRecovery = { amount: 0, reference: '' };
  newCorrectiveAction = { controlId: '', description: '' };
  uploadDocumentType = 'Evidence';
  uploadConfidentiality = 'Internal';
  selectedFile: File | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly caseService: CaseService,
    private readonly investigationsService: InvestigationsService,
    private readonly recoveriesService: RecoveriesService,
    private readonly correctiveActionsService: CorrectiveActionsService,
    private readonly controlsService: ControlsService,
    private readonly documentsService: DocumentsService,
    readonly authService: AuthService
  ) {
    this.caseId = this.route.snapshot.paramMap.get('id') ?? '';
  }

  ngOnInit(): void {
    this.loadCase();
    this.loadInvestigations();
    this.loadRecoveries();
    this.loadCorrectiveActions();
    this.loadDocuments();
    this.controlsService.getAll().subscribe((controls) => this.controls.set(controls));
  }

  loadCase(): void {
    this.caseService.getById(this.caseId).subscribe((c) => this.caseDetail.set(c));
  }

  loadInvestigations(): void {
    this.investigationsService.getForCase(this.caseId).subscribe((items) => this.investigations.set(items));
  }

  loadRecoveries(): void {
    this.recoveriesService.getForCase(this.caseId).subscribe((items) => this.recoveries.set(items));
  }

  loadCorrectiveActions(): void {
    this.correctiveActionsService.getForCase(this.caseId).subscribe((items) => this.correctiveActions.set(items));
  }

  loadDocuments(): void {
    this.documentsService.getAll(this.caseId).subscribe((items) => this.documents.set(items));
  }

  changeStatus(): void {
    if (!this.newStatus) {
      return;
    }
    this.errorMessage.set(null);
    this.caseService.changeStatus(this.caseId, { newStatus: this.newStatus, reason: this.statusReason }).subscribe({
      next: () => {
        this.newStatus = '';
        this.statusReason = '';
        this.loadCase();
      },
      error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to change case status.')
    });
  }

  createInvestigation(): void {
    this.investigationsService
      .create(this.caseId, {
        findings: this.newInvestigation.findings || null,
        recommendation: this.newInvestigation.recommendation || null
      })
      .subscribe({
        next: () => {
          this.newInvestigation = { findings: '', recommendation: '' };
          this.loadInvestigations();
        },
        error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to capture investigation.')
      });
  }

  approveInvestigation(id: string, approve: boolean): void {
    this.investigationsService.approve(id, { approve, comments: null }).subscribe({
      next: () => this.loadInvestigations(),
      error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to approve investigation.')
    });
  }

  captureRecovery(): void {
    this.recoveriesService
      .capture(this.caseId, { amount: this.newRecovery.amount, reference: this.newRecovery.reference || null })
      .subscribe({
        next: () => {
          this.newRecovery = { amount: 0, reference: '' };
          this.loadRecoveries();
          this.loadCase();
        },
        error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to capture recovery.')
      });
  }

  approveWriteOff(id: string): void {
    const reason = window.prompt('Write-off reason:');
    if (!reason) {
      return;
    }
    this.recoveriesService.approveWriteOff(id, { reason }).subscribe({
      next: () => this.loadRecoveries(),
      error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to approve write-off.')
    });
  }

  createCorrectiveAction(): void {
    this.correctiveActionsService
      .create(this.caseId, {
        controlId: this.newCorrectiveAction.controlId,
        description: this.newCorrectiveAction.description
      })
      .subscribe({
        next: () => {
          this.newCorrectiveAction = { controlId: '', description: '' };
          this.loadCorrectiveActions();
        },
        error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to create corrective action.')
      });
  }

  verifyCorrectiveAction(id: string): void {
    const documentId = window.prompt('Evidence Document Id to verify against:');
    if (!documentId) {
      return;
    }
    this.correctiveActionsService.verify(id, { evidenceDocumentId: documentId }).subscribe({
      next: () => this.loadCorrectiveActions(),
      error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to verify corrective action.')
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  uploadDocument(): void {
    if (!this.selectedFile) {
      return;
    }
    this.documentsService
      .upload(this.selectedFile, this.uploadDocumentType, this.uploadConfidentiality, this.caseId)
      .subscribe({
        next: () => {
          this.selectedFile = null;
          this.loadDocuments();
        },
        error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to upload document.')
      });
  }

  controlName(controlId: string): string {
    return this.controls().find((c) => c.id === controlId)?.name ?? controlId;
  }
}
