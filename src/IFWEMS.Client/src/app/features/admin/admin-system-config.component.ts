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
import { MatTooltipModule } from '@angular/material/tooltip';
import { SystemConfigService } from '../../core/admin/system-config.service';
import { SystemSettingDto } from '../../core/models/system-config.models';

@Component({
  selector: 'app-admin-system-config',
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
    MatTooltipModule
  ],
  templateUrl: './admin-system-config.component.html',
  styles: [`
    .admin-system-config {
      .create-card h2 { margin: 0 0 16px 0; font-size: 1.1rem; font-weight: 600; }
      .error-text { color: #c62828; font-size: 0.85rem; margin: 0 0 12px 0; }
      .full-width { grid-column: 1 / -1; }
      .inline-field { width: 100%; }
      code { background: rgba(0, 0, 0, 0.06); padding: 2px 6px; border-radius: 4px; font-size: 0.85em; }
      h2 { margin: 0 0 16px 0; font-size: 1.05rem; font-weight: 600; }
    }
  `]
})
export class AdminSystemConfigComponent implements OnInit {
  readonly settings = signal<SystemSettingDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showCreateForm = signal(false);
  readonly editingId = signal<string | null>(null);

  newSetting = {
    key: '',
    value: '',
    category: '',
    description: ''
  };

  editValue = '';
  editDescription = '';

  constructor(private readonly systemConfigService: SystemConfigService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.systemConfigService.getAll().subscribe({
      next: (settings) => {
        this.settings.set(settings);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  get groupedSettings(): { category: string; items: SystemSettingDto[] }[] {
    const groups = new Map<string, SystemSettingDto[]>();
    for (const setting of this.settings()) {
      const list = groups.get(setting.category) ?? [];
      list.push(setting);
      groups.set(setting.category, list);
    }
    return Array.from(groups.entries()).map(([category, items]) => ({ category, items }));
  }

  toggleCreateForm(): void {
    this.showCreateForm.set(!this.showCreateForm());
    this.errorMessage.set(null);
  }

  createSetting(): void {
    if (!this.newSetting.key || !this.newSetting.category) {
      return;
    }
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.systemConfigService
      .create({
        key: this.newSetting.key,
        value: this.newSetting.value,
        category: this.newSetting.category,
        description: this.newSetting.description || null
      })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.newSetting = { key: '', value: '', category: '', description: '' };
          this.showCreateForm.set(false);
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.message ?? 'Failed to create setting.');
        }
      });
  }

  startEdit(setting: SystemSettingDto): void {
    this.editingId.set(setting.id);
    this.editValue = setting.value;
    this.editDescription = setting.description ?? '';
    this.errorMessage.set(null);
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(id: string): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.systemConfigService
      .update(id, { value: this.editValue, description: this.editDescription || null })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.editingId.set(null);
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.message ?? 'Failed to update setting.');
        }
      });
  }

  deleteSetting(setting: SystemSettingDto): void {
    if (!confirm(`Delete system setting "${setting.key}"?`)) {
      return;
    }
    this.systemConfigService.delete(setting.id).subscribe({
      next: () => this.load(),
      error: (err) => this.errorMessage.set(err?.error?.message ?? 'Failed to delete setting.')
    });
  }
}
