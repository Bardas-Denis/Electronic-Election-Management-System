import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { SetupService } from '../services/setup.service';
import {
  adminGuard,
  alreadyConfiguredGuard,
  authGuard,
  electionManagerGuard,
  homeGuestGuard,
  setupGuard
} from './auth.guard';

describe('authentication guards', () => {
  it('redirects anonymous users from protected routes to login', () => {
    const { router } = configureAuth({ loggedIn: false });

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('allows authenticated users through the general guard', () => {
    configureAuth({ loggedIn: true });

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBe(true);
  });

  it('blocks non-admin users from administrator routes', () => {
    const { router } = configureAuth({ loggedIn: true, admin: false });

    const result = TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never));

    expect(result).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/elections']);
  });

  it('allows election managers into management routes', () => {
    configureAuth({ loggedIn: true, manager: true });

    const result = TestBed.runInInjectionContext(
      () => electionManagerGuard({} as never, {} as never)
    );

    expect(result).toBe(true);
  });

  it('redirects authenticated users away from the guest homepage', () => {
    const { router, redirectTree } = configureAuth({ loggedIn: true });

    const result = TestBed.runInInjectionContext(
      () => homeGuestGuard({} as never, [] as never)
    );

    expect(result).toBe(redirectTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/elections']);
  });

  describe('setupGuard', () => {
    it('allows navigation when instance is configured', async () => {
      configureSetup({ configured: true });

      const guard$ = TestBed.runInInjectionContext(() => setupGuard({} as never, {} as never)) as any;
      guard$.subscribe((result: any) => {
        expect(result).toBe(true);
      });
    });

    it('redirects to /setup when instance is unconfigured', async () => {
      const { router, redirectTree } = configureSetup({ configured: false });

      const guard$ = TestBed.runInInjectionContext(() => setupGuard({} as never, {} as never)) as any;
      guard$.subscribe((result: any) => {
        expect(result).toBe(redirectTree);
        expect(router.createUrlTree).toHaveBeenCalledWith(['/setup']);
      });
    });

    it('fails-open on error and allows access', async () => {
      configureSetup({ error: true });

      const guard$ = TestBed.runInInjectionContext(() => setupGuard({} as never, {} as never)) as any;
      guard$.subscribe((result: any) => {
        expect(result).toBe(true);
      });
    });
  });

  describe('alreadyConfiguredGuard', () => {
    it('redirects to /login when instance is already configured', async () => {
      const { router, redirectTree } = configureSetup({ configured: true });

      const guard$ = TestBed.runInInjectionContext(
        () => alreadyConfiguredGuard({} as never, {} as never)
      ) as any;
      guard$.subscribe((result: any) => {
        expect(result).toBe(redirectTree);
        expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
      });
    });

    it('allows access to /setup when instance is unconfigured', async () => {
      configureSetup({ configured: false });

      const guard$ = TestBed.runInInjectionContext(
        () => alreadyConfiguredGuard({} as never, {} as never)
      ) as any;
      guard$.subscribe((result: any) => {
        expect(result).toBe(true);
      });
    });

    it('redirects to /login on status error', async () => {
      const { router, redirectTree } = configureSetup({ error: true });

      const guard$ = TestBed.runInInjectionContext(
        () => alreadyConfiguredGuard({} as never, {} as never)
      ) as any;
      guard$.subscribe((result: any) => {
        expect(result).toBe(redirectTree);
        expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
      });
    });
  });
});

function configureAuth(options: {
  loggedIn: boolean;
  admin?: boolean;
  manager?: boolean;
}) {
  const auth = {
    isLoggedIn: vi.fn(() => options.loggedIn),
    isAdmin: vi.fn(() => options.admin ?? false),
    canManageElections: vi.fn(() => options.manager ?? false)
  };
  const redirectTree = { redirected: true };
  const router = {
    navigate: vi.fn(),
    createUrlTree: vi.fn(() => redirectTree)
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: router }
    ]
  });
  return { auth, router, redirectTree };
}

function configureSetup(options: {
  configured?: boolean;
  error?: boolean;
}) {
  const setup = {
    getStatus: vi.fn(() =>
      options.error
        ? throwError(() => new Error('Network error'))
        : of({ configured: options.configured ?? false })
    )
  };
  const redirectTree = { redirected: true };
  const router = {
    navigate: vi.fn(),
    createUrlTree: vi.fn(() => redirectTree)
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SetupService, useValue: setup },
      { provide: Router, useValue: router }
    ]
  });
  return { setup, router, redirectTree };
}

