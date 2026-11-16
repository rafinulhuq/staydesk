import { TestBed } from '@angular/core/testing';
import { CanMatchFn, Route, Router, UrlSegment, UrlTree, provideRouter } from '@angular/router';
import { Role } from './auth.models';
import { AuthService } from './auth.service';
import { roleGuard } from './role.guard';

describe('roleGuard', () => {
  function run(role: Role, user: { roles: Role[] } | null): boolean | UrlTree {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isLoggedIn: () => user !== null,
            hasRole: (r: Role) => user?.roles.includes(r) ?? false,
          },
        },
      ],
    });
    const segments = [new UrlSegment('admin', {}), new UrlSegment('bookings', {})];
    return TestBed.runInInjectionContext(
      () =>
        roleGuard(role)({} as Route, segments, {} as Parameters<CanMatchFn>[2]) as
          boolean | UrlTree,
    );
  }

  it('sends a logged-out visitor to login with the page they wanted', () => {
    const result = run('Staff', null);

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(
      '/login?returnUrl=%2Fadmin%2Fbookings',
    );
  });

  it('sends a user without the role to the home page', () => {
    const result = run('Staff', { roles: ['Guest'] });

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/');
  });

  it('lets a user with the role in', () => {
    expect(run('Staff', { roles: ['Admin', 'Staff'] })).toBe(true);
  });
});
