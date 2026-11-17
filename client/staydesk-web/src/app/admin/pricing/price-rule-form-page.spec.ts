import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { PriceRuleFormPage } from './price-rule-form-page';

describe('PriceRuleFormPage', () => {
  let http: HttpTestingController;

  async function openNew(): Promise<PriceRuleFormPage> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'admin/pricing/new', component: PriceRuleFormPage },
          { path: 'admin/pricing', children: [] },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        provideNativeDateAdapter(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/admin/pricing/new?propertyId=p1', PriceRuleFormPage);
    http.expectOne('/api/v1/properties/p1?includeInactive=true').flush({
      rooms: [{ id: 'r1', roomNumber: '101' }],
    });
    return page;
  }

  // PRICE-2: the API rejects fields a rule type doesn't use, so a form switched from
  // Seasonal to Weekend must not send the dates typed before.
  it('sends only the fields of the chosen rule type', async () => {
    const page = await openNew();
    page.form.setValue({
      name: 'Wochenende',
      roomId: '',
      type: 'Seasonal',
      percentChange: 15,
      startDate: new Date(2026, 11, 1),
      endDate: new Date(2026, 11, 24),
      minNights: 3,
      daysBeforeCheckIn: null,
    });
    page.form.controls.type.setValue('Weekend');

    page.submit();

    expect(http.expectOne({ method: 'POST', url: '/api/v1/price-rules' }).request.body).toEqual({
      propertyId: 'p1',
      roomId: null,
      name: 'Wochenende',
      type: 'Weekend',
      percentChange: 15,
      startDate: null,
      endDate: null,
      minNights: null,
      daysBeforeCheckIn: null,
    });
  });

  // PRICE-8: the preview validates the unsaved rule; its errors come back nested as "rule.*".
  it('shows preview errors for the draft rule under the rule fields', async () => {
    const page = await openNew();
    page.form.patchValue({ name: 'Weihnachtsmarkt', type: 'Seasonal', percentChange: 20 });
    page.previewForm.patchValue({
      checkIn: new Date(2026, 11, 10),
      checkOut: new Date(2026, 11, 12),
    });

    page.runPreview();
    const request = http.expectOne({ method: 'POST', url: '/api/v1/pricing/preview' });
    expect(request.request.body).toMatchObject({
      roomId: 'r1',
      checkIn: '2026-12-10',
      checkOut: '2026-12-12',
      rule: { propertyId: 'p1', name: 'Weihnachtsmarkt' },
    });
    request.flush(
      { errors: { 'rule.startDate': ["'Start Date' must not be empty."] } },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(page.form.controls.startDate.getError('server')).toBe("'Start Date' must not be empty.");
    expect(page.previewState()).toBe('invalid');
  });
});
