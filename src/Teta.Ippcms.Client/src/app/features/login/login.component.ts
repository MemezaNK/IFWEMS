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
    <div class="login-shell">
      <!-- Decorative brand panel -- built entirely from layered gradients, a dot-grid texture and
           two softly drifting blurred shapes rather than a stock photo, so it reads as a designed
           background image with no external asset or licensing dependency, and stays on-brand
           with the dark-navy palette already used for the app shell's sidenav. Hidden on narrow
           screens where there isn't room for it next to the form. -->
      <div class="login-visual" aria-hidden="true">
        <div class="visual-blob visual-blob-a"></div>
        <div class="visual-blob visual-blob-b"></div>
        <div class="visual-content">
          <div class="visual-mark">T</div>
          <h2 class="visual-title">TETA IPPCMS</h2>
          <p class="visual-tagline">A single system of record for portfolio, procurement, contract and learnership monitoring across TETA.</p>
          <ul class="visual-points">
            <li><mat-icon>account_tree</mat-icon> Portfolio &amp; programme governance</li>
            <li><mat-icon>shopping_cart</mat-icon> Procurement &amp; contract lifecycle</li>
            <li><mat-icon>fact_check</mat-icon> Monitoring, evaluation &amp; compliance</li>
          </ul>
        </div>
      </div>

      <div class="login-panel">
        <div class="login-card">
          <div class="login-brand">
            <div class="brand-mark">T</div>
            <div class="brand-text"><span class="brand-title">TETA IPPCMS</span><span class="brand-subtitle">Integrated Portfolio, Procurement &amp; Contract Management</span></div>
          </div>
          <h1 class="login-heading">{{ step() === 'credentials' ? 'Welcome back' : step() === 'mfa' ? 'Verify your identity' : 'Set up MFA' }}</h1>

          @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
          @if (info()) { <div class="info-banner">{{ info() }}</div> }
          @if (error()) { <div class="warn-banner" role="alert">{{ error() }}</div> }

          @switch (step()) {
            @case ('credentials') {
              <form (ngSubmit)="login()">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Username or e-mail</mat-label>
                  <mat-icon matPrefix>person</mat-icon>
                  <input matInput name="username" [(ngModel)]="username" required autocomplete="username" autofocus />
                </mat-form-field>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Password</mat-label>
                  <mat-icon matPrefix>lock</mat-icon>
                  <input matInput name="password" type="password" [(ngModel)]="password" required autocomplete="current-password" />
                </mat-form-field>
                <button mat-flat-button color="primary" class="full-width login-submit" [disabled]="busy() || !username || !password">Sign in</button>
              </form>
              <p class="login-footnote">Use your platform account (the same credentials as IFWEMS). Access to TETA requires a TETA role.</p>
            }
            @case ('mfa') {
              <form (ngSubmit)="verify()">
                <p class="muted small">Enter the 6-digit code from your authenticator app.</p>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Verification code</mat-label>
                  <mat-icon matPrefix>verified_user</mat-icon>
                  <input matInput name="code" [(ngModel)]="code" required inputmode="numeric" maxlength="6" autocomplete="one-time-code" />
                </mat-form-field>
                <button mat-flat-button color="primary" class="full-width login-submit" [disabled]="busy() || code.length !== 6">Verify</button>
              </form>
            }
            @case ('enrol') {
              <p class="small">Your role requires multi-factor authentication. Add this account to an authenticator app (Microsoft Authenticator, Google Authenticator&hellip;):</p>
              <dl class="dl small">
                <dt>Account</dt><dd>{{ username }}</dd>
                <dt>Secret key</dt><dd class="mono" style="word-break: break-all">{{ enrolment()?.secret }}</dd>
              </dl>
              <p class="small muted">Or open this link on a phone with an authenticator installed: <a [href]="enrolment()?.otpAuthUri">otpauth link</a></p>
              <form (ngSubmit)="confirm()">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Code from the app</mat-label>
                  <mat-icon matPrefix>verified_user</mat-icon>
                  <input matInput name="code" [(ngModel)]="code" required inputmode="numeric" maxlength="6" />
                </mat-form-field>
                <button mat-flat-button color="primary" class="full-width login-submit" [disabled]="busy() || code.length !== 6">Activate MFA and sign in</button>
              </form>
            }
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }

    .login-shell { min-height: 100vh; display: flex; background: #fff; }

    /* ---------- Left: decorative brand panel ---------- */
    .login-visual {
      position: relative;
      flex: 1 1 55%;
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
      background:
        radial-gradient(circle at 15% 20%, rgba(96, 165, 250, .35), transparent 45%),
        radial-gradient(circle at 85% 12%, rgba(124, 58, 237, .28), transparent 42%),
        radial-gradient(circle at 75% 88%, rgba(37, 99, 235, .38), transparent 45%),
        linear-gradient(160deg, #0b1526 0%, #101c34 55%, #132844 100%);
    }
    /* Fine dot-grid texture so the panel reads as a designed surface, not a flat gradient fill. */
    .login-visual::before {
      content: '';
      position: absolute; inset: 0;
      background-image: radial-gradient(rgba(255, 255, 255, .09) 1px, transparent 1px);
      background-size: 22px 22px;
      opacity: .6;
    }
    /* Soft vignette keeps the copy readable regardless of where the blobs drift. */
    .login-visual::after {
      content: '';
      position: absolute; inset: 0;
      background: linear-gradient(180deg, rgba(11, 21, 38, 0) 0%, rgba(11, 21, 38, .38) 100%);
    }
    .visual-blob {
      position: absolute;
      border-radius: 50%;
      filter: blur(70px);
      opacity: .55;
      animation: blob-drift 22s ease-in-out infinite alternate;
    }
    .visual-blob-a { width: 340px; height: 340px; top: -80px; left: -60px; background: #2563eb; }
    .visual-blob-b { width: 300px; height: 300px; bottom: -100px; right: -60px; background: #7c3aed; animation-delay: -8s; }
    @keyframes blob-drift {
      from { transform: translate(0, 0) scale(1); }
      to { transform: translate(30px, 24px) scale(1.08); }
    }
    @media (prefers-reduced-motion: reduce) { .visual-blob { animation: none; } }

    .visual-content { position: relative; z-index: 1; color: #fff; max-width: 420px; padding: 0 48px; animation: visual-in .5s ease-out; }
    .visual-mark {
      width: 56px; height: 56px; border-radius: 14px;
      background: linear-gradient(135deg, #2563eb, #60a5fa);
      display: flex; align-items: center; justify-content: center;
      font-size: 26px; font-weight: 700; color: #fff;
      box-shadow: 0 10px 26px rgba(37, 99, 235, .35);
      margin-bottom: 22px;
    }
    .visual-title { font-size: 27px; font-weight: 600; margin: 0 0 10px; letter-spacing: -.01em; }
    .visual-tagline { color: rgba(255, 255, 255, .72); font-size: 14px; line-height: 1.55; margin: 0 0 30px; }
    .visual-points { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 13px; }
    .visual-points li { display: flex; align-items: center; gap: 10px; font-size: 13px; color: rgba(255, 255, 255, .85); }
    .visual-points .mat-icon { font-size: 18px; width: 18px; height: 18px; color: #93c5fd; flex: 0 0 auto; }

    @media (max-width: 900px) { .login-visual { display: none; } }

    /* ---------- Right: sign-in form ---------- */
    .login-panel { flex: 1 1 45%; display: flex; align-items: center; justify-content: center; padding: 32px 24px; background: #fff; }
    .login-card { width: 100%; max-width: 380px; animation: card-in .4s ease-out; }
    @keyframes card-in { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: translateY(0); } }
    @keyframes visual-in { from { opacity: 0; transform: translateX(-8px); } to { opacity: 1; transform: translateX(0); } }

    .login-brand { display: flex; align-items: center; gap: 12px; margin-bottom: 26px; }
    .login-brand .brand-mark {
      width: 40px; height: 40px; border-radius: 10px;
      background: linear-gradient(135deg, #2563eb, #60a5fa);
      color: #fff; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 18px; flex: 0 0 auto;
    }
    .login-brand .brand-text { display: flex; flex-direction: column; line-height: 1.25; min-width: 0; }
    .login-brand .brand-title { font-size: 15px; font-weight: 600; color: #1f2933; }
    .login-brand .brand-subtitle { font-size: 11.5px; color: #6b7684; }

    .login-heading { font-size: 22px; font-weight: 600; margin: 0 0 18px; letter-spacing: -.01em; color: #1f2933; }
    .login-card mat-progress-bar { border-radius: 4px; overflow: hidden; margin-bottom: 16px; }
    .login-card .info-banner, .login-card .warn-banner { margin-bottom: 16px; }
    .login-card .mat-icon[matPrefix] { color: #9aa5b1; margin-right: 4px; font-size: 20px; width: 20px; height: 20px; }
    .login-submit { height: 44px; font-size: 14px; margin-top: 4px; }
    .login-footnote { font-size: 12px; color: #6b7684; margin-top: 18px; line-height: 1.5; }
  `]
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
