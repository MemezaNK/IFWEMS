import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AuthService } from '../../core/auth.service';
import { LoginResult, MfaEnrolment } from '../../core/models';

type Step = 'credentials' | 'mfa' | 'enrol';

/** Sign-in with the shared platform account (same credentials as IFWEMS) plus TOTP MFA for privileged roles (SEC-001/002). */
@Component({
  selector: 'teta-login',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div style="min-height: 100vh; display: flex; align-items: center; justify-content: center; background: linear-gradient(135deg, #0f3d6e, #1f6fb2)">
      <div class="card" style="width: 420px; max-width: 92vw; padding: 28px">
        <h1 style="font-size: 22px; margin: 0 0 4px">TETA IPPCMS</h1>
        <p class="muted" style="margin-top: 0">Integrated Portfolio, Procurement and Contract Management</p>
        @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
        @if (info()) { <div class="info-banner">{{ info() }}</div> }
        @if (error()) { <div class="warn-banner" role="alert">{{ error() }}</div> }

        @switch (step()) {
          @case ('credentials') {
            <form (ngSubmit)="login()">
              <mat-form-field class="full-width"><mat-label>Username or e-mail</mat-label>
                <input matInput name="username" [(ngModel)]="username" required autocomplete="username" autofocus /></mat-form-field>
              <mat-form-field class="full-width"><mat-label>Password</mat-label>
                <input matInput name="password" type="password" [(ngModel)]="password" required autocomplete="current-password" /></mat-form-field>
              <button mat-flat-button color="primary" class="full-width" [disabled]="busy() || !username || !password">Sign in</button>
            </form>
            <p class="small muted" style="margin-top: 16px">Use your platform account (the same credentials as IFWEMS). Access to TETA requires a TETA role.</p>
          }
          @case ('mfa') {
            <form (ngSubmit)="verify()">
              <p>Enter the 6-digit code from your authenticator app.</p>
              <mat-form-field class="full-width"><mat-label>Verification code</mat-label>
                <input matInput name="code" [(ngModel)]="code" required inputmode="numeric" maxlength="6" autocomplete="one-time-code" /></mat-form-field>
              <button mat-flat-button color="primary" class="full-width" [disabled]="busy() || code.length !== 6">Verify</button>
            </form>
          }
          @case ('enrol') {
            <p>Your role requires multi-factor authentication. Add this account to an authenticator app (Microsoft Authenticator, Google Authenticator…):</p>
            <dl class="dl small">
              <dt>Account</dt><dd>{{ username }}</dd>
              <dt>Secret key</dt><dd class="mono" style="word-break: break-all">{{ enrolment()?.secret }}</dd>
            </dl>
            <p class="small muted">Or open this link on a phone with an authenticator installed: <a [href]="enrolment()?.otpAuthUri">otpauth link</a></p>
            <form (ngSubmit)="confirm()">
              <mat-form-field class="full-width"><mat-label>Code from the app</mat-label>
                <input matInput name="code" [(ngModel)]="code" required inputmode="numeric" maxlength="6" /></mat-form-field>
              <button mat-flat-button color="primary" class="full-width" [disabled]="busy() || code.length !== 6">Activate MFA and sign in</button>
            </form>
          }
        }
      </div>
    </div>
  `
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly step = signal<Step>('credentials');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly info = signal<string | null>(null);
  readonly enrolment = signal<MfaEnrolment | null>(null);
  username = '';
  password = '';
  code = '';
  private challenge = '';

  ngOnInit(): void {
    const reason = this.route.snapshot.queryParamMap.get('reason');
    if (reason === 'inactive') this.info.set('You were signed out after a period of inactivity.');
    if (reason === 'expired') this.info.set('Your session has expired. Please sign in again.');
    if (this.auth.isAuthenticated()) this.router.navigateByUrl('/');
  }

  login(): void {
    this.run(this.auth.login(this.username.trim(), this.password), r => {
      if (r.mfaEnrolmentRequired && r.challengeToken) {
        this.challenge = r.challengeToken;
        this.auth.beginEnrolment(r.challengeToken).subscribe(e => {
          this.enrolment.set(e);
          this.challenge = e.challengeToken;
          this.step.set('enrol');
        });
      } else if (r.mfaRequired && r.challengeToken) {
        this.challenge = r.challengeToken;
        this.step.set('mfa');
      }
    });
  }

  verify(): void {
    this.run(this.auth.verifyMfa(this.challenge, this.code), () => undefined);
  }

  confirm(): void {
    this.run(this.auth.confirmEnrolment(this.challenge, this.code), () => undefined);
  }

  private run(call: ReturnType<AuthService['login']>, onPending: (r: LoginResult) => void): void {
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: r => {
        this.busy.set(false);
        if (!r.succeeded) {
          this.error.set(r.message ?? 'Sign-in failed.');
          this.code = '';
          return;
        }
        if (r.accessToken) {
          this.password = '';
          this.router.navigateByUrl(this.auth.isSupplier() ? '/portal' : '/');
          return;
        }
        onPending(r);
      },
      error: () => this.busy.set(false)
    });
  }
}
