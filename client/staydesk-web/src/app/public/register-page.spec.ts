import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { RegisterPage } from './register-page';

describe('RegisterPage', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
      ],
    }),
  );

  it('shows the API field error under the email field', async () => {
    const fixture = TestBed.createComponent(RegisterPage);
    // Rendered first, as for a real user: binding the controls re-runs validation.
    await fixture.whenStable();
    fixture.componentInstance.form.setValue({
      firstName: 'Anna',
      lastName: 'Keller',
      email: 'anna.keller@example.de',
      password: 'Sommer2026',
    });

    fixture.componentInstance.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/register')
      .flush(
        { code: 'validation_failed', errors: { email: ['This email is already registered.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    const errors = [...fixture.nativeElement.querySelectorAll('mat-error')].map((e) =>
      e.textContent.trim(),
    );
    expect(errors).toEqual(['This email is already registered.']);
  });

  it('does not call the API while the password misses the AUTH-1 rule', () => {
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.componentInstance.form.setValue({
      firstName: 'Anna',
      lastName: 'Keller',
      email: 'anna.keller@example.de',
      password: 'sommerzeit',
    });

    fixture.componentInstance.submit();

    TestBed.inject(HttpTestingController).expectNone('/api/v1/auth/register');
    expect(fixture.componentInstance.form.controls.password.hasError('pattern')).toBe(true);
  });
});
