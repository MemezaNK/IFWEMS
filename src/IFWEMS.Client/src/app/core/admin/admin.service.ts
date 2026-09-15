import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateRoleRequest,
  CreateUserRequest,
  OrgUnitDto,
  RoleDto,
  UserDto
} from '../models/admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin`;

  constructor(private readonly http: HttpClient) {}

  getUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.baseUrl}/users`);
  }

  createUser(request: CreateUserRequest): Observable<UserDto> {
    return this.http.post<UserDto>(`${this.baseUrl}/users`, request);
  }

  deactivateUser(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/users/${id}/deactivate`, {});
  }

  getRoles(): Observable<RoleDto[]> {
    return this.http.get<RoleDto[]>(`${this.baseUrl}/roles`);
  }

  createRole(request: CreateRoleRequest): Observable<RoleDto> {
    return this.http.post<RoleDto>(`${this.baseUrl}/roles`, request);
  }

  getOrgUnits(): Observable<OrgUnitDto[]> {
    return this.http.get<OrgUnitDto[]>(`${this.baseUrl}/org-units`);
  }

  createOrgUnit(request: Omit<OrgUnitDto, 'id' | 'isActive'>): Observable<OrgUnitDto> {
    return this.http.post<OrgUnitDto>(`${this.baseUrl}/org-units`, { ...request, id: null, isActive: true });
  }
}
