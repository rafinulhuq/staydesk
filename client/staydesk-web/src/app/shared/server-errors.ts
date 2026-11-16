import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';

// Puts the API's field-level validation messages (RFC 9457 `errors`) on the matching
// controls, so each message shows under its own field. A list item's error
// ("imageUrls[1]") goes to the list's control. Returns false if no message found a
// control, so the caller can show the error another way.
export function applyServerErrors(form: FormGroup, error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400 || !error.error?.errors) {
    return false;
  }

  const byControl = new Map<string, string[]>();
  for (const [field, messages] of Object.entries(error.error.errors as Record<string, string[]>)) {
    const name = field.replace(/\[\d+\]$/, '');
    byControl.set(name, [...(byControl.get(name) ?? []), ...messages]);
  }

  let applied = false;
  for (const [name, messages] of byControl) {
    const control = form.get(name);
    if (control) {
      control.setErrors({ ...control.errors, server: messages.join(' ') });
      control.markAsTouched();
      applied = true;
    }
  }
  return applied;
}

// The API's `code` extension (e.g. invalid_credentials), or null.
export function problemCode(error: unknown): string | null {
  return error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
    ? error.error.code
    : null;
}
