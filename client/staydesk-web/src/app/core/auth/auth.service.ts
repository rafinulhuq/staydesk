import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  Observable,
  catchError,
  finalize,
  firstValueFrom,
  map,
  of,
  shareReplay,
  switchMap,
  tap,
  throwError,
} from 'rxjs';
import { AuthTokens, CurrentUser, LoginRequest, RegisterRequest, Role } from './auth.models';

// D3: the access token lives only in memory; the refresh token survives reloads in
// localStorage, because the SPA and API may sit on different hosts.
const refreshTokenKey = 'staydesk.refreshToken';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly accessTokenSignal = signal<string | null>(null);
  private accessTokenExpiresAt = 0;
  private readonly userSignal = signal<CurrentUser | null>(null);
  private refreshInFlight: Observable<string> | null = null;

  readonly accessToken = this.accessTokenSignal.asReadonly();
  readonly user = this.userSignal.asReadonly();
  readonly isLoggedIn = computed(() => this.userSignal() !== null);

  hasRole(role: Role): boolean {
    return this.userSignal()?.roles.includes(role) ?? false;
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.http
      .post<AuthTokens>('/api/v1/auth/login', request)
      .pipe(switchMap((tokens) => this.startSession(tokens)));
  }

  register(request: RegisterRequest): Observable<CurrentUser> {
    return this.http
      .post<AuthTokens>('/api/v1/auth/register', request)
      .pipe(switchMap((tokens) => this.startSession(tokens)));
  }

  logout(): Observable<void> {
    const refreshToken = localStorage.getItem(refreshTokenKey);
    const revoke = refreshToken
      ? this.http.post<void>('/api/v1/auth/logout', { refreshToken })
      : of(undefined);
    // The local session ends even if the server call fails; the token then just expires.
    return revoke.pipe(
      catchError(() => of(undefined)),
      finalize(() => this.clearSession()),
    );
  }

  // Concurrent callers share one refresh request: a second rotation with the same token
  // would look like token reuse to the API (AUTH-2) and log the user out everywhere.
  refreshAccessToken(): Observable<string> {
    const refreshToken = localStorage.getItem(refreshTokenKey);
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }

    this.refreshInFlight ??= this.http
      .post<AuthTokens>('/api/v1/auth/refresh', { refreshToken })
      .pipe(
        tap((tokens) => this.storeTokens(tokens)),
        map((tokens) => tokens.accessToken),
        catchError((error: unknown) => {
          // Only a 401 means the refresh token is dead. A 429 or a network error doesn't,
          // and dropping the token then would log the user out for nothing.
          if (error instanceof HttpErrorResponse && error.status === 401) {
            this.clearSession();
          }
          return throwError(() => error);
        }),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay(1),
      );
    return this.refreshInFlight;
  }

  // For connections that read the token once instead of going through the HTTP interceptor
  // (the SignalR hub, on every connect and reconnect): the current token, or a refreshed one
  // if it expires within the next minute.
  freshAccessToken(): Promise<string> {
    const token = this.accessTokenSignal();
    return token && this.accessTokenExpiresAt - Date.now() > 60_000
      ? Promise.resolve(token)
      : firstValueFrom(this.refreshAccessToken());
  }

  // Runs once at startup, before the first navigation, so guards see the restored user.
  async restoreSession(): Promise<void> {
    if (!localStorage.getItem(refreshTokenKey)) {
      return;
    }
    // On failure the user starts logged out; refreshAccessToken() already dropped the
    // token if the API rejected it, otherwise the next reload tries again.
    await firstValueFrom(
      this.refreshAccessToken().pipe(
        switchMap(() => this.loadUser()),
        catchError(() => of(null)),
      ),
    );
  }

  private startSession(tokens: AuthTokens): Observable<CurrentUser> {
    this.storeTokens(tokens);
    return this.loadUser();
  }

  private loadUser(): Observable<CurrentUser> {
    return this.http
      .get<CurrentUser>('/api/v1/auth/me')
      .pipe(tap((user) => this.userSignal.set(user)));
  }

  private storeTokens(tokens: AuthTokens): void {
    this.accessTokenSignal.set(tokens.accessToken);
    this.accessTokenExpiresAt = Date.parse(tokens.accessTokenExpiresAt);
    localStorage.setItem(refreshTokenKey, tokens.refreshToken);
  }

  private clearSession(): void {
    this.accessTokenSignal.set(null);
    this.userSignal.set(null);
    localStorage.removeItem(refreshTokenKey);
  }
}
