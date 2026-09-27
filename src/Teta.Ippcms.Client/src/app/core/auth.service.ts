import { Injectable, NgZone, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { CurrentUser, LoginResult, MfaEnrolment } from './models';

const TOKEN_KEY = 'teta_access_token';

/**
 * Session handling for TETA (independent of IFWEMS: own token key, issuer and audience). The token
 * lives in sessionStorage; the server also enforces inactivity timeout and revocation (SEC-009).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private inactivityTimer?: ReturnType<typeof setTimeout>;

  readonly user = signal<CurrentUser | null>(null);
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly isSupplier = computed(() => this.user()?.roles.includes('Supplier') ?? false);

  get token(): string | null {
    try { return sessionStorage.getItem(TOKEN_KEY); } catch { return null; }
  }

  private setToken(token: string | null): void {
    try {
      if (token) sessionStorage.setItem(TOKEN_KEY, token); else sessionStorage.removeItem(TOKEN_KEY);
    } catch { /* storage unavailable */ }
  }

  login(username: string, password: string): Observable<LoginResult> {
    return this.api.post<LoginResult>('auth/login', { username, password }).pipe(tap(r => this.accept(r)));
  }

  verifyMfa(challengeToken: string, code: string): Observable<LoginResult> {
    return this.api.post<LoginResult>('auth/mfa/verify', { challengeToken, code }).pipe(tap(r => this.accept(r)));
  }

  beginEnrolment(challengeToken: string | null): Observable<MfaEnrolment> {
    return this.api.post<MfaEnrolment>('auth/mfa/enrol', { challengeToken });
  }

  confirmEnrolment(challengeToken: string, code: string): Observable<LoginResult> {
    return this.api.post<LoginResult>('auth/mfa/enrol/confirm', { challengeToken, code }).pipe(tap(r => this.accept(r)));
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.api.post<void>('auth/change-password', { currentPassword, newPassword });
  }

  /** Restores the session on page load (token in sessionStorage). */
  restore(): Promise<void> {
    if (!this.token) return Promise.resolve();
    return new Promise(resolve => {
      this.api.get<CurrentUser>('auth/me').subscribe({
        next: u => { this.user.set(u); this.armInactivity(); resolve(); },
        error: () => { this.clear(); resolve(); }
      });
    });
  }

  logout(reason?: string): void {
    const hadToken = !!this.token;
    if (hadToken) this.api.post('auth/logout').subscribe({ error: () => undefined });
    this.clear();
    this.router.navigate(['/login'], { queryParams: reason ? { reason } : {} });
  }

  clear(): void {
    this.setToken(null);
    this.user.set(null);
    if (this.inactivityTimer) clearTimeout(this.inactivityTimer);
  }

  has(permission: string): boolean {
    return this.user()?.permissions.includes(permission) ?? false;
  }

  hasAny(...permissions: string[]): boolean {
    const granted = this.user()?.permissions ?? [];
    return permissions.length === 0 || permissions.some(p => granted.includes(p));
  }

  inRole(role: string): boolean {
    return this.user()?.roles.includes(role) ?? false;
  }

  /** Called on user activity to postpone the client-side inactivity logout. */
  touch(): void {
    if (this.user()) this.armInactivity();
  }

  private accept(result: LoginResult): void {
    if (result.succeeded && result.accessToken) {
      this.setToken(result.accessToken);
      if (result.user) this.user.set(result.user);
      this.armInactivity();
    }
  }

  private armInactivity(): void {
    if (this.inactivityTimer) clearTimeout(this.inactivityTimer);
    const minutes = this.user()?.inactivityMinutes ?? 20;
    this.zone.runOutsideAngular(() => {
      this.inactivityTimer = setTimeout(() => this.zone.run(() => this.logout('inactive')), minutes * 60_000);
    });
  }
}
