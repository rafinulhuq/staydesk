import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { HomePage } from './public/home-page';
import { MyBookingsPage } from './account/my-bookings-page';
import { DashboardPage } from './admin/dashboard-page';

describe('app routes', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter(routes)] }));

  it.each([
    ['/', HomePage],
    ['/account', MyBookingsPage],
    ['/admin', DashboardPage],
  ])('lazy-loads the area for %s', async (url, page) => {
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl(url, page)).toBeInstanceOf(page);
  });

  it('sends unknown URLs to the home page', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/no-such-page');
    expect(TestBed.inject(Router).url).toBe('/');
  });
});
