import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService.restoreSession', () => {
  function restoreWith(status: number, statusText: string): Promise<void> {
    localStorage.setItem('staydesk.refreshToken', 'stored-token');
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const restored = TestBed.inject(AuthService).restoreSession();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/refresh')
      .flush({ code: 'x' }, { status, statusText });
    return restored;
  }

  afterEach(() => localStorage.clear());

  it('drops the refresh token when the API rejects it (401)', async () => {
    await restoreWith(401, 'Unauthorized');

    expect(localStorage.getItem('staydesk.refreshToken')).toBeNull();
  });

  // Each full page load refreshes once, and refresh shares the auth rate limit (NFR-3).
  it('keeps the refresh token when the refresh is rate limited (429)', async () => {
    await restoreWith(429, 'Too Many Requests');

    expect(localStorage.getItem('staydesk.refreshToken')).toBe('stored-token');
    expect(TestBed.inject(AuthService).isLoggedIn()).toBe(false);
  });
});
