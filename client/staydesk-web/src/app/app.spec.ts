import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { DashboardPage } from './admin/dashboard-page';
import { MyBookingsPage } from './account/my-bookings-page';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { Role } from './core/auth/auth.models';
import { HomePage } from './public/home-page';
import { LoginPage } from './public/login-page';

describe('app routes', () => {
  function setup(roles: Role[] | null): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideTranslateService(),
        {
          provide: AuthService,
          useValue: {
            isLoggedIn: () => roles !== null,
            hasRole: (r: Role) => !!roles?.includes(r),
          },
        },
      ],
    });
  }

  it('shows the home page to everyone', async () => {
    setup(null);
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/', HomePage)).toBeInstanceOf(HomePage);
  });

  it('opens My bookings for a guest at /account', async () => {
    setup(['Guest']);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/account', MyBookingsPage);
    expect(TestBed.inject(Router).url).toBe('/account/bookings');
  });

  it('opens the dashboard for staff', async () => {
    setup(['Staff']);
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/admin', DashboardPage)).toBeInstanceOf(DashboardPage);
  });

  it('sends a logged-out visitor from /admin to login', async () => {
    setup(null);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/admin', LoginPage);
    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Fadmin');
  });

  it('sends unknown URLs to the home page', async () => {
    setup(null);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/no-such-page');
    expect(TestBed.inject(Router).url).toBe('/');
  });
});
