import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { AdminService } from '../../core/admin/admin.service';
import { CreateUserRequest, OrgUnitDto, RoleDto, UserDto } from '../../core/models/admin.models';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCardModule,
    MatChipsModule
  ],
  templateUrl: './admin-users.component.html',
  styleUrl: './admin-users.component.scss'
})
export class AdminUsersComponent implements OnInit {
  readonly users = signal<UserDto[]>([]);
  readonly roles = signal<RoleDto[]>([]);
  readonly orgUnits = signal<OrgUnitDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly displayedColumns = ['username', 'displayName', 'email', 'roles', 'isActive', 'actions'];

  readonly newUser: CreateUserRequest = {
    username: '',
    email: '',
    displayName: '',
    password: '',
    orgUnitId: null,
    roleNames: []
  };

  constructor(private readonly adminService: AdminService) {}

  ngOnInit(): void {
    this.loadUsers();
    this.adminService.getRoles().subscribe({ next: (roles) => this.roles.set(roles) });
    this.adminService.getOrgUnits().subscribe({ next: (orgUnits) => this.orgUnits.set(orgUnits) });
  }

  loadUsers(): void {
    this.isLoading.set(true);
    this.adminService.getUsers().subscribe({
      next: (users) => {
        this.users.set(users);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  createUser(): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.adminService.createUser(this.newUser).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.newUser.username = '';
        this.newUser.email = '';
        this.newUser.displayName = '';
        this.newUser.password = '';
        this.newUser.orgUnitId = null;
        this.newUser.roleNames = [];
        this.loadUsers();
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.title ?? 'Failed to create user.');
      }
    });
  }

  deactivateUser(user: UserDto): void {
    this.adminService.deactivateUser(user.id).subscribe({ next: () => this.loadUsers() });
  }
}
