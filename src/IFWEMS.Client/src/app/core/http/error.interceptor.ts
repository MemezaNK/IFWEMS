import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { ToastService } from '../shared/toast.service';
import { extractErrorMessage } from './error-message.util';

/**
 * Global HTTP error handling: whenever any API call fails, surface the server's
 * error/validation message (or a sensible fallback) as a toast so the user always
 * sees why an action failed, regardless of whether the calling component has its
 * own inline error handling.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      // Blob-bodied error responses (e.g. report/document downloads) must be read
      // asynchronously as text before the usual JSON extraction logic can apply.
      if (error.error instanceof Blob && error.error.type.includes('json')) {
        return from(error.error.text()).pipe(
          switchMap((text) => {
            const parsedError = new HttpErrorResponse({
              error: text,
              headers: error.headers,
              status: error.status,
              statusText: error.statusText,
              url: error.url ?? undefined
            });
            toast.showError(extractErrorMessage(parsedError));
            return throwError(() => error);
          })
        );
      }

      toast.showError(extractErrorMessage(error));
      return throwError(() => error);
    })
  );
};
