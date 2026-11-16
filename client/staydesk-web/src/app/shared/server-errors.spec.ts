import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import { applyServerErrors } from './server-errors';

describe('applyServerErrors', () => {
  function validationError(errors: Record<string, string[]>): HttpErrorResponse {
    return new HttpErrorResponse({
      status: 400,
      error: { code: 'validation_failed', errors },
    });
  }

  it('puts errors for list items ("imageUrls[1]") on the list control', () => {
    const form = new FormGroup({ imageUrls: new FormControl('') });

    const applied = applyServerErrors(
      form,
      validationError({
        'imageUrls[0]': ['Must be an absolute http or https URL.'],
        'imageUrls[2]': ['Must be at most 1000 characters.'],
      }),
    );

    expect(applied).toBe(true);
    expect(form.controls.imageUrls.getError('server')).toBe(
      'Must be an absolute http or https URL. Must be at most 1000 characters.',
    );
  });

  it('reports false when no field matches, so the page shows a general error', () => {
    const form = new FormGroup({ roomNumber: new FormControl('') });

    expect(applyServerErrors(form, validationError({ propertyId: ['Required.'] }))).toBe(false);
  });
});
