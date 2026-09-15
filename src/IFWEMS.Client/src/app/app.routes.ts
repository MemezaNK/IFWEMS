import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/landing/landing.component').then((m) => m.LandingComponent)
  },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent)
  },
  {
    path: 'cases',
    canActivate: [authGuard],
    loadComponent: () => import('./features/cases/case-list.component').then((m) => m.CaseListComponent)
  },
  {
    path: 'compliance/check',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/compliance/compliance-check.component').then((m) => m.ComplianceCheckComponent)
  },
  {
    path: 'compliance/override',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/compliance/emergency-override.component').then((m) => m.EmergencyOverrideComponent)
  },
  {
    path: 'admin',
    canActivate: [authGuard],
    data: { roles: ['SystemAdministrator'] },
    loadComponent: () => import('./features/admin/admin-home.component').then((m) => m.AdminHomeComponent)
  },
  {
    path: 'admin/users',
    canActivate: [authGuard],
    data: { roles: ['SystemAdministrator'] },
    loadComponent: () => import('./features/admin/admin-users.component').then((m) => m.AdminUsersComponent)
  },
  {
    path: 'admin/roles',
    canActivate: [authGuard],
    data: { roles: ['SystemAdministrator'] },
    loadComponent: () => import('./features/admin/admin-roles.component').then((m) => m.AdminRolesComponent)
  },
  {
    path: 'admin/org-units',
    canActivate: [authGuard],
    data: { roles: ['SystemAdministrator'] },
    loadComponent: () => import('./features/admin/admin-org-units.component').then((m) => m.AdminOrgUnitsComponent)
  },
  { path: '**', redirectTo: 'dashboard' }
];
