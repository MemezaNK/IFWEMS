export interface UserDto {
  id: string;
  username: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
}

export interface CreateUserRequest {
  username: string;
  email: string;
  displayName: string;
  password: string;
  orgUnitId: string | null;
  roleNames: string[];
}

export interface RoleDto {
  id: string;
  name: string;
  description: string | null;
  permissions: string[];
}

export interface CreateRoleRequest {
  name: string;
  description: string | null;
  permissionCodes: string[];
}

export interface OrgUnitDto {
  id: string;
  code: string;
  name: string;
  level: string;
  parentOrgUnitId: string | null;
  isActive: boolean;
}
