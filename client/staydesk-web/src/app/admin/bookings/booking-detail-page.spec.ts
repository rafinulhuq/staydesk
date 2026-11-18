import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { BookingDetail, BookingStatus } from '../../shared/booking.models';
import { BookingDetailPage } from './booking-detail-page';

function booking(status: BookingStatus, changes: Partial<BookingDetail> = {}): BookingDetail {
  return {
    id: 'b1',
    reference: 'SD-2026-000042',
    roomId: 'r1',
    roomNumber: '12',
    propertyId: 'p1',
    propertyName: 'Hotel Spreeblick',
    checkIn: '2026-11-18',
    checkOut: '2026-11-20',
    guests: 2,
    guestFullName: 'Anna Becker',
    guestEmail: 'anna.becker@example.de',
    guestPhone: null,
    specialRequests: null,
    status,
    totalPrice: 240,
    priceBreakdown: [
      { date: '2026-11-18', basePrice: 120, rules: [], price: 120 },
      { date: '2026-11-19', basePrice: 120, rules: [], price: 120 },
    ],
    cancellationFee: null,
    refundAmount: null,
    feeWaived: false,
    createdAt: '2026-11-01T10:00:00Z',
    expiresAt: null,
    confirmedAt: '2026-11-01T10:05:00Z',
    checkedInAt: null,
    checkedOutAt: null,
    cancelledAt: null,
    internalNotes: null,
    ...changes,
  };
}

// BO-2: status actions offered by status, decided by the server, and internal notes.
describe('BookingDetailPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  // Staff answer every confirm dialog with yes.
  const confirmDialogs = { open: vi.fn(() => ({ afterClosed: () => of(true) })) };

  async function open(loaded: BookingDetail): Promise<BookingDetailPage> {
    confirmDialogs.open.mockClear();
    TestBed.configureTestingModule({
      providers: [
        { provide: MatDialog, useValue: confirmDialogs },
        provideRouter([{ path: 'admin/bookings/:id', component: BookingDetailPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/admin/bookings/b1', BookingDetailPage);
    http.expectOne('/api/v1/bookings/b1').flush(loaded);
    await harness.fixture.whenStable();
    return page;
  }

  function buttons(): string[] {
    return [...harness.fixture.nativeElement.querySelectorAll('.actions-bar button')].map(
      (b: HTMLButtonElement) => b.textContent!.trim(),
    );
  }

  // Every loaded or changed booking reloads its history; tests that don't look at it answer empty.
  afterEach(() => {
    http.match('/api/v1/bookings/b1/history').forEach((request) => request.flush([]));
    http.verify();
  });

  it('offers check-in, no-show and cancel for a Confirmed booking, and check-out once checked in', async () => {
    const page = await open(booking('Confirmed'));
    expect(buttons()).toEqual([
      'admin.booking.checkIn',
      'admin.booking.markNoShow',
      'account.bookings.cancel',
    ]);

    page.checkIn();
    http
      .expectOne({ method: 'POST', url: '/api/v1/bookings/b1/check-in' })
      .flush(booking('CheckedIn'));
    await harness.fixture.whenStable();

    expect(buttons()).toEqual(['admin.booking.checkOut']);
  });

  it('says why when the server refuses a status change', async () => {
    const page = await open(
      booking('Confirmed', { checkIn: '2026-12-01', checkOut: '2026-12-03' }),
    );

    page.checkIn();
    http
      .expectOne('/api/v1/bookings/b1/check-in')
      .flush({ code: 'invalid_status_transition' }, { status: 400, statusText: 'Bad Request' });
    await harness.fixture.whenStable();

    expect(harness.fixture.nativeElement.textContent).toContain('admin.booking.notice.notAllowed');
  });

  it('cancels with the fee waived after staff confirm the full refund', async () => {
    const page = await open(booking('Confirmed'));
    page.waiveFee.set(true);

    page.cancel(page.booking()!);
    http
      .expectOne('/api/v1/bookings/b1/cancellation-quote')
      .flush({ amountPaid: 240, fee: 120, refund: 120, freeUntil: '2026-11-16T14:00:00Z' });
    expect(confirmDialogs.open).toHaveBeenCalledWith(expect.anything(), {
      data: expect.objectContaining({
        messageKey: 'admin.booking.cancelWaived',
        params: expect.objectContaining({ refund: '€240.00' }),
      }),
    });

    const cancel = http.expectOne('/api/v1/bookings/b1/cancel');
    expect(cancel.request.body).toEqual({ waiveFee: true });
    cancel.flush(booking('Cancelled', { feeWaived: true, cancellationFee: 0, refundAmount: 240 }));
  });

  it('saves the internal notes', async () => {
    const page = await open(booking('Confirmed', { internalNotes: 'Cot requested' }));
    expect(page.notes.value).toBe('Cot requested');

    page.notes.setValue('Cot requested. Late arrival.');
    page.saveNotes();

    const save = http.expectOne({ method: 'PATCH', url: '/api/v1/bookings/b1/notes' });
    expect(save.request.body).toEqual({ internalNotes: 'Cot requested. Late arrival.' });
    save.flush(booking('Confirmed', { internalNotes: 'Cot requested. Late arrival.' }));
  });

  it('shows the history with the fields each change touched', async () => {
    await open(booking('CheckedIn'));

    http.expectOne('/api/v1/bookings/b1/history').flush([
      {
        id: 'a1',
        timestamp: '2026-11-01T10:00:00Z',
        userId: 'g1',
        userDisplayName: 'Anna Becker',
        entityName: 'Booking',
        entityId: 'b1',
        action: 'Created',
        changes: { Status: { old: null, new: 'Pending' } },
        correlationId: 'c1',
      },
      {
        id: 'a2',
        timestamp: '2026-11-18T09:12:00Z',
        userId: 's1',
        userDisplayName: 'Jonas Becker',
        entityName: 'Booking',
        entityId: 'b1',
        action: 'Updated',
        changes: { Status: { old: 'Confirmed', new: 'CheckedIn' } },
        correlationId: 'c2',
      },
    ]);
    await harness.fixture.whenStable();

    const items = [
      ...harness.fixture.nativeElement.querySelectorAll('.timeline li'),
    ] as HTMLElement[];
    expect(items).toHaveLength(2);
    expect(items[0].querySelector('sd-audit-changes')).toBeNull();
    expect(items[1].textContent).toContain('Jonas Becker');
    expect(items[1].querySelector('sd-audit-changes')?.textContent).toMatch(
      /Status\s*Confirmed\s*CheckedIn/,
    );
  });
});
