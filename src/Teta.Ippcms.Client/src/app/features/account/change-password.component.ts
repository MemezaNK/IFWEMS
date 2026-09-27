import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../core/auth.service';

/** Self-service password change (SEC-001). */
@Component({
  selector: 'teta-change-password',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule],
  template: `
    <div class="page">
      <div class="page-header"><h1>Change password</h1></div>
      <form class="card" style="max-width: 480px" (ngSubmit)="submit()">
        <mat-form-field appearance="outline" style="width: 100%">
          <mat-label>Current password</mat-label>
          <input matInput type="password" name="current" required [(ngModel)]="currentPassword" />
        </mat-form-field>
        <mat-form-field appearance="outline" style="width: 100%">
          <mat-label>New password</mat-label>
          <input matInput type="password" name="new" required minlength="12" [(ngModel)]="newPassword" />
          <mat-hint>At least 12 characters, mixing upper/lower case, numbers and symbols.</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline" style="width: 100%">
          <mat-label>Confirm new password</mat-label>
          <input matInput type="password" name="confirm" required [(ngModel)]="confirmPassword" />
        </mat-form-field>
        @if (error) { <p style="color: #b91c1c">{{ error }}</p> }
        <button mat-flat-button color="primary" type="submit"><mat-icon>key</mat-icon> Change password</button>
      </form>
    </div>
  `
})
export class ChangePasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly snack = inject(MatSnackBar);
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  error = '';

  submit(): void {
    this.error = '';
    if (!this.currentPassword || !this.newPassword) return;
    if (this.newPassword !== this.confirmPassword) { this.error = 'The new password and confirmation do not match.'; return; }
    this.auth.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.snack.open('Password changed. Please sign in again.', 'OK', { duration: 4000 });
        this.currentPassword = this.newPassword = this.confirmPassword = '';
        this.auth.logout();
      },
      error: err => { this.error = err?.error?.message ?? 'Could not change the password. Check your current password and try again.'; }
    });
  }
}
