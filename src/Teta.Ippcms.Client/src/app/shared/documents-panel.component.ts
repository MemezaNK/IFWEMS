import { Component, Input, OnChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { P } from '../core/models';
import { Column, DataTableComponent } from './data-table.component';
import { openForm } from './form-dialog.component';

/**
 * Documents and evidence attached to a record (SRS §14): versioned uploads with classification,
 * malware scan and hash on the server; audited downloads; evidence verification by an authorised
 * user other than the uploader.
 */
@Component({
  selector: 'teta-documents',
  standalone: true,
  imports: [DataTableComponent, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule, FormsModule],
  template: `
    <div class="toolbar-row">
      <h2 style="margin:0">{{ evidenceMode ? 'Evidence' : 'Documents' }}</h2>
      <span class="spacer"></span>
      @if (canUpload) {
        <input #file type="file" hidden (change)="upload(file.files); file.value = ''" />
        <button mat-stroked-button type="button" (click)="file.click()"><mat-icon>upload_file</mat-icon> Upload {{ evidenceMode ? 'evidence' : 'document' }}</button>
      }
    </div>
    <teta-data-table [columns]="evidenceMode ? evidenceColumns : documentColumns" [rows]="rows" [actions]="actions" [filterable]="false"
                     [paginate]="false" [exportable]="false" emptyText="Nothing attached yet." />
    <ng-template #actions let-row>
      <button mat-icon-button type="button" (click)="download(row)" title="Download"><mat-icon>download</mat-icon></button>
      @if (evidenceMode && canVerify && row.verificationStatus === 'Pending') {
        <button mat-icon-button type="button" (click)="verify(row, true)" title="Verify"><mat-icon>verified</mat-icon></button>
        <button mat-icon-button type="button" (click)="verify(row, false)" title="Reject"><mat-icon>block</mat-icon></button>
      }
    </ng-template>
  `
})
export class DocumentsPanelComponent implements OnChanges {
  @Input({ required: true }) parentType!: string;
  @Input({ required: true }) parentId!: string;
  @Input() evidenceMode = false;
  @Input() documentType = 'Supporting document';

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  rows: any[] = [];
  documentColumns: Column[] = [
    { key: 'title', label: 'Title' }, { key: 'documentType', label: 'Type' }, { key: 'documentVersion', label: 'Version', type: 'number' },
    { key: 'classification', label: 'Classification', type: 'status' }, { key: 'uploadedBy', label: 'Uploaded by' },
    { key: 'uploadedAtUtc', label: 'Uploaded', type: 'datetime' }, { key: 'expiryDate', label: 'Expires', type: 'date' }
  ];
  evidenceColumns: Column[] = [
    { key: 'fileName', label: 'File' }, { key: 'evidenceType', label: 'Evidence type' }, { key: 'source', label: 'Source' },
    { key: 'verificationStatus', label: 'Status', type: 'status' }, { key: 'verifiedBy', label: 'Verified by' },
    { key: 'uploadedBy', label: 'Uploaded by' }, { key: 'uploadedAtUtc', label: 'Uploaded', type: 'datetime' }
  ];

  get canUpload(): boolean {
    return this.auth.hasAny(P.documentsUpload, P.portalAccess, P.performanceCapture);
  }

  get canVerify(): boolean {
    return this.auth.hasAny(P.evidenceVerify, P.performanceVerify);
  }

  ngOnChanges(): void {
    this.load();
  }

  load(): void {
    const path = this.evidenceMode ? 'documents/evidence' : 'documents';
    this.api.get<any[]>(path, { parentType: this.parentType, parentId: this.parentId }).subscribe(r => (this.rows = r));
  }

  async upload(files: FileList | null): Promise<void> {
    const file = files?.[0];
    if (!file) return;
    const meta = await openForm(this.dialog, {
      title: `Upload ${file.name}`,
      fields: [
        { key: 'documentType', label: this.evidenceMode ? 'Evidence type' : 'Document type', required: true },
        { key: 'title', label: 'Title' },
        { key: 'classification', label: 'Classification', type: 'select', required: true,
          options: ['Public', 'Internal', 'Confidential', 'Restricted'].map(v => ({ value: v, label: v })) },
        { key: 'expiryDate', label: 'Expiry date', type: 'date' }
      ],
      value: { documentType: this.documentType, classification: 'Internal', title: file.name },
      submitLabel: 'Upload'
    }, '560px');
    if (!meta) return;
    const form = new FormData();
    form.append('File', file, file.name);
    form.append('ParentType', this.parentType);
    form.append('ParentId', this.parentId);
    form.append('DocumentType', String(meta['documentType']));
    form.append('Classification', String(meta['classification']));
    if (meta['title']) form.append('Title', String(meta['title']));
    if (meta['expiryDate']) form.append('ExpiryDate', String(meta['expiryDate']));
    if (this.evidenceMode) form.append('EvidenceType', String(meta['documentType']));
    this.api.upload(this.evidenceMode ? 'documents/evidence' : 'documents', form).subscribe(() => {
      this.snack.open('Uploaded and scanned.', 'OK', { duration: 3000 });
      this.load();
    });
  }

  download(row: any): void {
    const id = this.evidenceMode ? row.documentId : row.id;
    this.api.download(`documents/${id}/content`).subscribe(r => ApiService.saveBlob(r, row.fileName ?? 'document'));
  }

  async verify(row: any, verified: boolean): Promise<void> {
    const result = await openForm(this.dialog, {
      title: verified ? 'Verify evidence' : 'Reject evidence',
      fields: [{ key: 'comment', label: 'Comment', type: 'textarea', required: !verified }],
      submitLabel: verified ? 'Verify' : 'Reject'
    }, '520px');
    if (!result) return;
    this.api.post(`documents/evidence/${row.id}/verify`, { verified, comment: result['comment'] }).subscribe(() => this.load());
  }
}
