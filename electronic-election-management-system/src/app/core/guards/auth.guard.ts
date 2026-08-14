import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { SetupService } from '../services/setup.service';

// Blocks the route if the user isn't logged in
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn()) {
    return true;
  }

  router.navigate(['/login']);
  return false;
};

// Blocks the route unless the user is logged in AND is an Admin
export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn() && authService.isAdmin()) {
    return true;
  }

  router.navigate(['/elections']);
  return false;
};

// Blocks the route unless the user is logged in AND can manage elections (Admin or ElectionManager)
export const electionManagerGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn() && authService.canManageElections()) {
    return true;
  }

  router.navigate(['/elections']);
  return false;
};

// Prevents loading the guest-only home page for authenticated users.
export const homeGuestGuard: CanMatchFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn()) {
    return router.createUrlTree(['/elections']);
  }

  return true;
};

// Blocks the app shell while the instance is unconfigured.
// Async because it needs an HTTP call to GET /api/setup/status.
// Fail-open on error so an already-running instance is never accidentally blocked.
export const setupGuard: CanActivateFn = () => {
  const setupService = inject(SetupService);
  const router = inject(Router);

  return setupService.getStatus().pipe(
    map(({ configured }) =>
      configured ? true : router.createUrlTree(['/setup'])
    ),
    catchError(() => of(true)) // fail-open: network error, assume configured
  );
};
