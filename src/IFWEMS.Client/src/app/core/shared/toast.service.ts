import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/**
 * Thin wrapper around MatSnackBar so error/success/info toasts look and
 * behave consistently across the whole app.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly snackBar = inject(MatSnackBar);

  showError(message: string): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 8000,
      panelClass: ['ifwems-toast', 'ifwems-toast-error'],
      horizontalPosition: 'right',
      verticalPosition: 'top'
    });
  }

  showSuccess(message: string): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 4000,
      panelClass: ['ifwems-toast', 'ifwems-toast-success'],
      horizontalPosition: 'right',
      verticalPosition: 'top'
    });
  }
}
