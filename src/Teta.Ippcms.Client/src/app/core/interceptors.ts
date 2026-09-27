import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  auth.touch();
  return next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
};

/** Turns RFC 7807 problem responses into readable messages; 401 ends the session. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const snack = inject(MatSnackBar);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes('auth/login')) {
        auth.clear();
        auth.logout('expired');
      } else if (!req.url.includes('notifications/unread-count')) {
        snack.open(problemMessage(error), 'Close', { duration: 8000, panelClass: 'error-snack' });
      }
      return throwError(() => error);
    })
  );
};

export function problemMessage(error: HttpErrorResponse): string {
  const body = error.error;
  if (body && typeof body === 'object') {
    const errors = body.errors as Record<string, string[]> | undefined;
    if (errors && Object.keys(errors).length) {
      return Object.entries(errors).map(([field, messages]) => `${field}: ${messages.join(' ')}`).join(' • ');
    }
    const code = body.code ? ` [${body.code}]` : '';
    if (body.detail) return `${body.detail}${code}`;
    if (body.title) return `${body.title}${code}`;
  }
  if (error.status === 0) return 'The server could not be reached. Check your connection.';
  if (error.status === 403) return 'You are not authorised to perform this action.';
  if (error.status === 404) return 'The record was not found or you do not have access to it.';
  return `Request failed (${error.status}).`;
}
