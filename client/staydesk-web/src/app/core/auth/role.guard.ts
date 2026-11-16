import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

// canMatch rather than canActivate, so a lazy area's code isn't even downloaded for
// someone who may not enter it. The API still checks every request (NFR-2).
export function roleGuard(role: Role): CanMatchFn {
  return (_route, segments) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isLoggedIn()) {
      const returnUrl = '/' + segments.map((s) => s.path).join('/');
      return router.createUrlTree(['/login'], { queryParams: { returnUrl } });
    }
    return auth.hasRole(role) ? true : router.createUrlTree(['/']);
  };
}
