import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthUser, LoginRequest, LoginResponse } from '../models/auth.models';

const TOKEN_STORAGE_KEY = 'ifwems_access_token';

// The backend's JwtTokenGenerator writes claims using System.Security.Claims.ClaimTypes,
// which serialize to these long XML-schema URIs (not the short "unique_name"/"role" names).
const CLAIM_NAMEIDENTIFIER = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const CLAIM_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const CLAIM_ROLE = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUserSignal = signal<AuthUser | null>(this.readUserFromToken());
  readonly currentUser = this.currentUserSignal.asReadonly();

  constructor(private readonly http: HttpClient) {}

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiBaseUrl}/auth/login`, request).pipe(
      tap((response) => {
        localStorage.setItem(TOKEN_STORAGE_KEY, response.accessToken);
        this.currentUserSignal.set(this.decodeToken(response.accessToken));
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    this.currentUserSignal.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  isAuthenticated(): boolean {
    return this.currentUserSignal() !== null;
  }

  hasRole(...roles: string[]): boolean {
    const user = this.currentUserSignal();
    return !!user && roles.some((role) => user.roles.includes(role));
  }

  private readUserFromToken(): AuthUser | null {
    const token = localStorage.getItem(TOKEN_STORAGE_KEY);
    return token ? this.decodeToken(token) : null;
  }

  private decodeToken(token: string): AuthUser | null {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const rolesClaim = payload[CLAIM_ROLE] ?? payload['role'] ?? [];
      return {
        userId: payload['sub'] ?? payload[CLAIM_NAMEIDENTIFIER] ?? '',
        username: payload[CLAIM_NAME] ?? payload['unique_name'] ?? payload['name'] ?? '',
        roles: Array.isArray(rolesClaim) ? rolesClaim : [rolesClaim]
      };
    } catch {
      return null;
    }
  }
}
