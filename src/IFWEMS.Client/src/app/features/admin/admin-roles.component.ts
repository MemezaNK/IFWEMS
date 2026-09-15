import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { AdminService } from '../../core/admin/admin.service';
import { CreateRoleRequest, RoleDto } from '../../core/models/admin.models';

@Component({
  selector: 'app-admin-roles',
  standalone: true,
  imports: [CommonModule, FormsModule, MatTableModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatCardModule, MatChipsModule],
  templateUrl: './admin-roles.component.html',
  styleUrl: './admin-roles.component.scss'
})
export class AdminRolesComponent implements OnInit {
  readonly roles = signal<RoleDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly displayedColumns = ['name', 'description', 'permissions'];

  permissionCodesInput = '';
  readonly newRole: CreateRoleRequest = { name: '', description: null, permissionCodes: [] };

  constructor(private readonly adminService: AdminService) {}

  ngOnInit(): void {
    this.loadRoles();
  }

  loadRoles(): void {
    this.isLoading.set(true);
    this.adminService.getRoles().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  createRole(): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.newRole.permissionCodes = this.permissionCodesInput
      .split(',')
      .map((code) => code.trim())
      .filter((code) => code.length > 0);

    this.adminService.createRole(this.newRole).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.newRole.name = '';
        this.newRole.description = null;
        this.newRole.permissionCodes = [];
        this.permissionCodesInput = '';
        this.loadRoles();
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.title ?? 'Failed to create role.');
      }
    });
  }
}
