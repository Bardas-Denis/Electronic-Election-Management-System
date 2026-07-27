import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../services/auth.service';
import { jwtInterceptor } from './jwt.interceptor';

describe('jwtInterceptor', () => {
  let httpClient: HttpClient;
  let httpTesting: HttpTestingController;
  let auth: {
    getToken: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    auth = {
      getToken: vi.fn(() => 'jwt-token'),
      logout: vi.fn()
    };
    router = { navigate: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([jwtInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router }
      ]
    });
    httpClient = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('adds the bearer token to outgoing requests', () => {
    httpClient.get('/api/elections').subscribe();

    const request = httpTesting.expectOne('/api/elections');
    expect(request.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    request.flush([]);
  });

  it('clears the session and redirects when the API rejects a revoked token', () => {
    httpClient.get('/api/elections').subscribe({ error: () => undefined });

    const request = httpTesting.expectOne('/api/elections');
    request.flush(
      { reason: 'revoked' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(auth.logout).toHaveBeenCalledOnce();
    expect(router.navigate).toHaveBeenCalledWith(
      ['/login'],
      { queryParams: { reason: 'role-changed' } }
    );
  });
});
