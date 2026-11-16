import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// A 401 from these means wrong credentials or a dead session, not an expired access token.
const noRefreshUrls = ['/api/v1/auth/login', '/api/v1/auth/register', '/api/v1/auth/refresh'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        noRefreshUrls.includes(request.url)
      ) {
        return throwError(() => error);
      }
      // Retry exactly once with the new token; a second 401 goes to the caller.
      // If the refresh token is dead (401), send the user to log in and come back. A 429
      // or network failure keeps the session; the error interceptor already said why.
      return auth.refreshAccessToken().pipe(
        catchError((refreshError: unknown) => {
          if (refreshError instanceof HttpErrorResponse && refreshError.status === 401) {
            void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
          }
          return throwError(() => error);
        }),
        switchMap((token) => next(withToken(request, token))),
      );
    }),
  );
};

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}
