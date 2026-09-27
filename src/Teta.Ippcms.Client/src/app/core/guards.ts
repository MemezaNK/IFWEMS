import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login']);
};

/** Route data: { permissions: string[] } — any one grants access. */
export const permissionGuard: CanActivateFn = route => {
  const auth = inject(AuthService);
  const needed = (route.data?.['permissions'] as string[] | undefined) ?? [];
  if (!auth.isAuthenticated()) return inject(Router).createUrlTree(['/login']);
  return auth.hasAny(...needed) ? true : inject(Router).createUrlTree(['/']);
};
