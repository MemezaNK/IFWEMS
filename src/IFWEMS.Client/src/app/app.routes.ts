import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
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
  { path: '**', redirectTo: 'dashboard' }
];
