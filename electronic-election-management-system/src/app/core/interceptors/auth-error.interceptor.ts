import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Catches 401s from the API (expired/invalid JWT) anywhere in the app,
// clears the stale session, and bounces the user back to /login.
// Without this, a request made after the token expires just fails silently
// and the current page is left showing empty/stale data.
export const authErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // /auth/login and /auth/register return 401/400 for bad credentials -
  // that's a form validation error, not a session expiring, so leave those
  // to the login/register components' own error handling untouched.
  const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/register');

  return next(req).pipe(
    catchError((error) => {
      if (error.status === 401 && !isAuthEndpoint && authService.isLoggedIn()) {
        authService.logout();

        if (!router.url.startsWith('/login')) {
          router.navigate(['/login'], { queryParams: { reason: 'session-expired' } });
        }
      }

      return throwError(() => error);
    })
  );
};
