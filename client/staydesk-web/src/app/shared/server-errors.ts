import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';

// Puts the API's field-level validation messages (RFC 9457 `errors`) on the matching
// controls, so each message shows under its own field. Returns false if the error
// wasn't a field validation error, so the caller can show it another way.
export function applyServerErrors(form: FormGroup, error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400 || !error.error?.errors) {
    return false;
  }

  const errors = error.error.errors as Record<string, string[]>;
  for (const [field, messages] of Object.entries(errors)) {
    const control = form.get(field);
    control?.setErrors({ ...control.errors, server: messages.join(' ') });
    control?.markAsTouched();
  }
  return true;
}

// The API's `code` extension (e.g. invalid_credentials), or null.
export function problemCode(error: unknown): string | null {
  return error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
    ? error.error.code
    : null;
}
