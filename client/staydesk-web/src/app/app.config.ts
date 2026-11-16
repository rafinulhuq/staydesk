import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { errorInterceptor } from './core/http/error.interceptor';
import { loadingInterceptor } from './core/http/loading.interceptor';
import { LanguageService } from './core/i18n/language.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Order matters: the first interceptor is the outermost, so the error snackbar
    // only sees what is left after the auth interceptor's refresh-and-retry.
    provideHttpClient(withInterceptors([loadingInterceptor, errorInterceptor, authInterceptor])),
    provideTranslateService({
      // Translation files bypass the interceptors: no bearer token, no loading bar.
      loader: provideTranslateHttpLoader({
        prefix: '/i18n/',
        suffix: '.json',
        useHttpBackend: true,
      }),
      fallbackLang: 'en',
    }),
    provideAppInitializer(() => {
      const language = inject(LanguageService);
      language.use(language.initialLanguage());
      return inject(AuthService).restoreSession();
    }),
  ],
};
