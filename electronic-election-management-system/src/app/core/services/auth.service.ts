import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, RegisterRequest, AuthResponse, CurrentUser } from '../models/auth.model';

const TOKEN_KEY = 'election_app_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // signal reactiv, folosit in toata aplicatia
  currentUser = signal<CurrentUser | null>(this.readUserFromToken());

  constructor(private http: HttpClient) {}

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

  private saveSession(res: AuthResponse): void {
    sessionStorage.setItem(TOKEN_KEY, res.token);
    this.currentUser.set(this.readUserFromToken());
  }

  private readUserFromToken(): CurrentUser | null {
    const token = sessionStorage.getItem(TOKEN_KEY);
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return {
        userId: payload['sub'] ?? payload['nameid'],
        email: payload['email'],
        role: payload['role'], 
      };
    } catch {
      return null;
    }
  }
}
