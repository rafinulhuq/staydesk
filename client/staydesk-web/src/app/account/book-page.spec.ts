import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { BookPage } from './book-page';

describe('BookPage', () => {
  let http: HttpTestingController;

  function quote(total: number) {
    return { roomId: 'r1', checkIn: '2026-12-10', checkOut: '2026-12-12', nights: [], total };
  }

  async function open(): Promise<BookPage> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'book/:roomId', component: BookPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        provideNativeDateAdapter(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl(
      '/book/r1?checkIn=2026-12-10&checkOut=2026-12-12&guests=2',
      BookPage,
    );
    http
      .expectOne('/api/v1/rooms/r1')
      .flush({ id: 'r1', propertyId: 'p1', roomNumber: '103', type: 'Double', maxGuests: 2 });
    http.expectOne('/api/v1/properties/p1').flush({ id: 'p1', name: 'Hotel Spreeblick' });
    http.expectOne('/api/v1/pricing/quote').flush(quote(240));
    harness.detectChanges();
    return page;
  }

  function fillDetails(page: BookPage): void {
    page.details.setValue({
      guestFullName: 'Lena Schmidt',
      guestEmail: 'lena.schmidt@example.com',
      guestPhone: '',
      specialRequests: '',
    });
  }

  // BOOK-1: the room's MaxGuests, known only after loading, limits the guest count.
  it('rejects more guests than the room sleeps', async () => {
    const page = await open();

    page.stay.controls.guests.setValue(3);

    expect(page.stay.controls.guests.hasError('max')).toBe(true);
  });

  it('does not reserve while the guest details are invalid', async () => {
    const page = await open();
    page.details.setValue({
      guestFullName: '',
      guestEmail: 'lena',
      guestPhone: '',
      specialRequests: '',
    });

    page.reserve();

    http.expectNone({ method: 'POST', url: '/api/v1/bookings' });
    expect(page.details.controls.guestFullName.hasError('required')).toBe(true);
    expect(page.details.controls.guestEmail.hasError('email')).toBe(true);
  });

  // PRICE-7 and BOOK-8: after price_changed the guest reserves again with the new total and
  // the same Idempotency-Key, since no booking was made.
  it('sends the shown total and, after a price change, the new one with the same key', async () => {
    const page = await open();
    fillDetails(page);

    page.reserve();
    const first = http.expectOne({ method: 'POST', url: '/api/v1/bookings' });
    expect(first.request.body).toMatchObject({
      roomId: 'r1',
      checkIn: '2026-12-10',
      guests: 2,
      expectedTotal: 240,
    });
    const key = first.request.headers.get('Idempotency-Key');
    expect(key).toMatch(/^[0-9a-f-]{36}$/);
    first.flush(
      { code: 'price_changed', quote: quote(260) },
      { status: 409, statusText: 'Conflict' },
    );

    expect(page.notice()).toBe('priceChanged');
    page.reserve();
    const second = http.expectOne({ method: 'POST', url: '/api/v1/bookings' });
    expect(second.request.body.expectedTotal).toBe(260);
    expect(second.request.headers.get('Idempotency-Key')).toBe(key);
  });
});
