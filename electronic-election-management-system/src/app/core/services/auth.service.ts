import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, RegisterRequest, AuthResponse, CurrentUser } from '../models/auth.model';

const TOKEN_KEY = 'election_app_token'; // sessionStorage, not localStorage - clears on tab close

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  // Reactive current-user state, read by guards/navbar/etc.
  currentUser = signal<CurrentUser | null>(this.readUserFromToken());

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap((res) => this.saveSession(res)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, request)
      .pipe(tap((res) => this.saveSession(res)));
  }

  logout(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    this.currentUser.set(null);
  }

  getToken(): string | null {
    return sessionStorage.getItem(TOKEN_KEY);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  isAdmin(): boolean {
    return this.currentUser()?.role === 'Admin';
  }

  /** True for Admin or ElectionManager — can create/edit/delete elections they own. */
  canManageElections(): boolean {
    const role = this.currentUser()?.role;
    return role === 'Admin' || role === 'ElectionManager';
  }

  private saveSession(res: AuthResponse): void {
    sessionStorage.setItem(TOKEN_KEY, res.token);
    this.currentUser.set(this.readUserFromToken());
  }

  // Decodes the JWT payload without verifying signature - UI display only
  private readUserFromToken(): CurrentUser | null {
    const token = sessionStorage.getItem(TOKEN_KEY);
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return {
        userId: payload['sub'] ?? payload['nameid'],
        email: payload['email'],
        role: payload['role'] || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      };
    } catch {
      return null; // malformed token, treat as logged out
    }
  }
}