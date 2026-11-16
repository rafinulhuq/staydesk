import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthTokens } from './auth.models';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const tokens = (n: number): AuthTokens => ({
  accessToken: `access-${n}`,
  accessTokenExpiresAt: '2026-11-17T10:15:00Z',
  refreshToken: `refresh-${n}`,
  refreshTokenExpiresAt: '2026-11-24T10:00:00Z',
});

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function logIn(): Promise<void> {
    const login = TestBed.inject(AuthService).login({ email: 'guest@example.com', password: 'x' });
    const done = new Promise<void>((resolve) => login.subscribe(() => resolve()));
    backend.expectOne('/api/v1/auth/login').flush(tokens(1));
    backend.expectOne('/api/v1/auth/me').flush({ id: '1', roles: ['Guest'] });
    await done;
  }

  it('adds the access token to API calls but not to other URLs', async () => {
    await logIn();

    http.get('/api/v1/properties').subscribe();
    http.get('/i18n/en.json').subscribe();

    expect(backend.expectOne('/api/v1/properties').request.headers.get('Authorization')).toBe(
      'Bearer access-1',
    );
    expect(backend.expectOne('/i18n/en.json').request.headers.has('Authorization')).toBe(false);
  });

  it('refreshes once for three concurrent 401s and retries each with the new token', async () => {
    await logIn();
    const results: string[] = [];
    for (const url of ['/api/v1/a', '/api/v1/b', '/api/v1/c']) {
      http.get<string>(url).subscribe((body) => results.push(body));
    }

    for (const url of ['/api/v1/a', '/api/v1/b', '/api/v1/c']) {
      backend.expectOne(url).flush(null, { status: 401, statusText: 'Unauthorized' });
    }
    const refresh = backend.expectOne('/api/v1/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush(tokens(2));

    for (const url of ['/api/v1/a', '/api/v1/b', '/api/v1/c']) {
      const retry = backend.expectOne(url);
      expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
      retry.flush(url);
    }
    expect(results.sort()).toEqual(['/api/v1/a', '/api/v1/b', '/api/v1/c']);
    expect(localStorage.getItem('staydesk.refreshToken')).toBe('refresh-2');
  });

  it('does not refresh when login itself returns 401', () => {
    let status = 0;
    TestBed.inject(AuthService)
      .login({ email: 'guest@example.com', password: 'wrong' })
      .subscribe({ error: (e) => (status = e.status) });

    backend
      .expectOne('/api/v1/auth/login')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    backend.expectNone('/api/v1/auth/refresh');
    expect(status).toBe(401);
  });

  it('ends the session and goes to login when the refresh fails', async () => {
    await logIn();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    let status = 0;
    http.get('/api/v1/a').subscribe({ error: (e) => (status = e.status) });

    backend.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend
      .expectOne('/api/v1/auth/refresh')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(TestBed.inject(AuthService).isLoggedIn()).toBe(false);
    expect(localStorage.getItem('staydesk.refreshToken')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login'], expect.anything());
  });

  it('passes a second 401 after the retry to the caller without refreshing again', async () => {
    await logIn();
    let status = 0;
    http.get('/api/v1/a').subscribe({ error: (e) => (status = e.status) });

    backend.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/auth/refresh').flush(tokens(2));
    backend.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });

    backend.expectNone('/api/v1/auth/refresh');
    expect(status).toBe(401);
  });
});
