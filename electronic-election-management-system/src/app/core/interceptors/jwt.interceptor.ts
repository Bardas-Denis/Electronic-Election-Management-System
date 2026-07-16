import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Attaches the JWT to every outgoing HTTP request and handles 401 responses (expired session / invalid token)
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();

  const cloned = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(cloned).pipe(
    catchError(err => {
      if (err.status === 401) {
        const backendReason = err.error?.reason;
        const uiReason = backendReason === 'revoked' ? 'role-changed' : 'session-expired';
        authService.logout();
        router.navigate(['/login'], { queryParams: { reason: uiReason } });
      }
      return throwError(() => err);
    })
  );
};
