import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCardModule } from '@angular/material/card';
import { AdminService } from '../../core/admin/admin.service';
import { OrgUnitDto } from '../../core/models/admin.models';

@Component({
  selector: 'app-admin-org-units',
  standalone: true,
  imports: [CommonModule, FormsModule, MatTableModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCardModule],
  templateUrl: './admin-org-units.component.html',
  styleUrl: './admin-org-units.component.scss'
})
export class AdminOrgUnitsComponent implements OnInit {
  readonly orgUnits = signal<OrgUnitDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly displayedColumns = ['code', 'name', 'level', 'parentOrgUnitId'];

  newOrgUnit: { code: string; name: string; level: string; parentOrgUnitId: string | null } = {
    code: '',
    name: '',
    level: '',
    parentOrgUnitId: null
  };

  constructor(private readonly adminService: AdminService) {}

  ngOnInit(): void {
    this.loadOrgUnits();
  }

  loadOrgUnits(): void {
    this.isLoading.set(true);
    this.adminService.getOrgUnits().subscribe({
      next: (orgUnits) => {
        this.orgUnits.set(orgUnits);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  createOrgUnit(): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.adminService.createOrgUnit(this.newOrgUnit).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.newOrgUnit = { code: '', name: '', level: '', parentOrgUnitId: null };
        this.loadOrgUnits();
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.title ?? 'Failed to create organisation unit.');
      }
    });
  }

  parentName(parentId: string | null): string {
    if (!parentId) {
      return '—';
    }
    return this.orgUnits().find((o) => o.id === parentId)?.name ?? '—';
  }
}
