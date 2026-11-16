import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateService } from '@ngx-translate/core';
import { catchError, throwError } from 'rxjs';

// Only failures a page can't explain in context get a snackbar. 400 (field errors),
// 401 (session handling), 404, 409 and 422 are shown by the page that made the call.
const snackbarKeys: Record<number, string> = {
  0: 'errors.network',
  403: 'errors.forbidden',
  429: 'errors.rateLimited',
};

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const snackBar = inject(MatSnackBar);
  const translate = inject(TranslateService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const key = snackbarKeys[error.status] ?? (error.status >= 500 ? 'errors.server' : null);
        if (key) {
          snackBar.open(translate.instant(key), translate.instant('common.close'), {
            duration: 6000,
          });
        }
      }
      return throwError(() => error);
    }),
  );
};
