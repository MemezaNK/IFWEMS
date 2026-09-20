import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationTemplatesService } from '../../core/admin/notification-templates.service';
import { NotificationTemplateDto } from '../../core/models/notification-template.models';

@Component({
  selector: 'app-admin-notification-templates',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatCheckboxModule,
    MatTooltipModule
  ],
  templateUrl: './admin-notification-templates.component.html',
  styleUrl: './admin-notification-templates.component.scss'
})
export class AdminNotificationTemplatesComponent implements OnInit {
  readonly templates = signal<NotificationTemplateDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showCreateForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly displayedColumns = ['code', 'subject', 'isActive', 'modifiedAtUtc', 'actions'];

  newTemplate = {
    code: '',
    subject: '',
    bodyHtml: '',
    isActive: true
  };

  editTemplate = {
    subject: '',
    bodyHtml: '',
    isActive: true
  };

  constructor(private readonly templatesService: NotificationTemplatesService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.templatesService.getAll().subscribe({
      next: (templates) => {
        this.templates.set(templates);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  toggleCreateForm(): void {
    this.showCreateForm.set(!this.showCreateForm());
    this.errorMessage.set(null);
  }

  createTemplate(): void {
    if (!this.newTemplate.code || !this.newTemplate.subject || !this.newTemplate.bodyHtml) {
      return;
    }
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.templatesService.create({ ...this.newTemplate }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.newTemplate = { code: '', subject: '', bodyHtml: '', isActive: true };
        this.showCreateForm.set(false);
        this.load();
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to create template.');
      }
    });
  }

  startEdit(template: NotificationTemplateDto): void {
    this.editingId.set(template.id);
    this.editTemplate = {
      subject: template.subject,
      bodyHtml: template.bodyHtml,
      isActive: template.isActive
    };
    this.errorMessage.set(null);
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(id: string): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.templatesService.update(id, { ...this.editTemplate }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.editingId.set(null);
        this.load();
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to update template.');
      }
    });
  }

  deleteTemplate(template: NotificationTemplateDto): void {
    if (!confirm(`Delete notification template "${template.code}"?`)) {
      return;
    }
    this.templatesService.delete(template.id).subscribe({
      next: () => this.load(),
      error: (err) => this.errorMessage.set(err?.error?.message ?? 'Failed to delete template.')
    });
  }
}
