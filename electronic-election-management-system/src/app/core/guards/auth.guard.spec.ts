import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../services/auth.service';
import { adminGuard, authGuard, electionManagerGuard, homeGuestGuard } from './auth.guard';

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
