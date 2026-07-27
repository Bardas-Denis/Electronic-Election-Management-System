import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { AuthResponse } from '../models/auth.model';
import { AuthService } from './auth.service';

const TOKEN_KEY = 'election_app_token';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('logs in, stores the token, and exposes decoded user claims', () => {
    const token = jwt({
      sub: 'user-id',
      email: 'voter@example.com',
      role: 'Voter',
      exp: Math.floor(Date.now() / 1000) + 3600
    });
    const response: AuthResponse = {
      token,
      userId: 'user-id',
      email: 'voter@example.com',
      role: 'Voter',
      expiresAt: new Date(Date.now() + 3_600_000).toISOString()
    };

    service.login({ email: response.email, password: 'password' }).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/auth/login`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'voter@example.com',
      password: 'password'
    });
    request.flush(response);

    expect(service.getToken()).toBe(token);
    expect(service.currentUser()).toEqual({
      userId: 'user-id',
      email: 'voter@example.com',
      role: 'Voter'
    });
    expect(service.isLoggedIn()).toBe(true);
  });

  it('clears an expired token when login state is checked', () => {
    sessionStorage.setItem(TOKEN_KEY, jwt({
      sub: 'expired-user',
      email: 'expired@example.com',
      role: 'Voter',
      exp: Math.floor(Date.now() / 1000) - 60
    }));

    expect(service.isLoggedIn()).toBe(false);
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull();
    expect(service.currentUser()).toBeNull();
  });

  it('recognizes administrator and election-manager permissions', () => {
    const adminToken = jwt({
      sub: 'admin-id',
      email: 'admin@example.com',
      role: 'Admin',
      exp: Math.floor(Date.now() / 1000) + 3600
    });
    const response: AuthResponse = {
      token: adminToken,
      userId: 'admin-id',
      email: 'admin@example.com',
      role: 'Admin',
      expiresAt: new Date(Date.now() + 3_600_000).toISOString()
    };

    service.register({ email: response.email, password: 'password' }).subscribe();
    http.expectOne(`${environment.apiUrl}/auth/register`).flush(response);

    expect(service.isAdmin()).toBe(true);
    expect(service.canManageElections()).toBe(true);
  });
});

function jwt(payload: Record<string, unknown>): string {
  return `${btoa('{}')}.${btoa(JSON.stringify(payload))}.signature`;
}
