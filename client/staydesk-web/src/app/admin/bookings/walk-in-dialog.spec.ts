import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDialogRef } from '@angular/material/dialog';
import { provideTranslateService } from '@ngx-translate/core';
import { GuestSummary } from '../../shared/booking.models';
import { WalkInDialog } from './walk-in-dialog';

const vogel: GuestSummary = {
  id: 'g1',
  firstName: 'Jonas',
  lastName: 'Vogel',
  email: 'jonas.vogel@example.de',
  phone: '+49 89 2345678',
};

// BOOK-9: a walk-in for an existing guest sends guestId; one for a new guest sends newGuest.
describe('WalkInDialog', () => {
  let http: HttpTestingController;
  let close: ReturnType<typeof vi.fn>;

  let fixture: ComponentFixture<WalkInDialog>;

  async function openWithStay(): Promise<WalkInDialog> {
    close = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        provideNativeDateAdapter(),
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(WalkInDialog);
    await fixture.whenStable();
    const dialog = fixture.componentInstance;
    http.expectOne('/api/v1/properties').flush([]);

    dialog.stay.controls.propertyId.setValue('p1');
    http.expectOne('/api/v1/properties/p1').flush({
      id: 'p1',
      rooms: [{ id: 'r1', roomNumber: '12', type: 'Double', maxGuests: 2 }],
    });
    dialog.stay.patchValue({
      roomId: 'r1',
      checkIn: new Date(2027, 2, 10),
      checkOut: new Date(2027, 2, 12),
    });
    http.expectOne('/api/v1/pricing/quote').flush({
      roomId: 'r1',
      checkIn: '2027-03-10',
      checkOut: '2027-03-12',
      nights: [{}, {}],
      total: 240,
    });
    return dialog;
  }

  afterEach(() => http.verify());

  it('books an existing guest found by name with their own details and the shown total', async () => {
    const dialog = await openWithStay();

    dialog.guestSearch.setValue('vogel');
    await new Promise((resolve) => setTimeout(resolve, 350));
    const search = http.expectOne((r) => r.url === '/api/v1/guests');
    expect(search.request.params.get('search')).toBe('vogel');
    search.flush([vogel]);
    dialog.chooseGuest(vogel);
    await fixture.whenStable();
    (fixture.nativeElement.querySelector('button[type=submit]') as HTMLButtonElement).click();

    const create = http.expectOne('/api/v1/bookings');
    expect(create.request.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    expect(create.request.body).toMatchObject({
      roomId: 'r1',
      checkIn: '2027-03-10',
      checkOut: '2027-03-12',
      guestId: 'g1',
      guestFullName: 'Jonas Vogel',
      guestEmail: 'jonas.vogel@example.de',
      guestPhone: '+49 89 2345678',
      expectedTotal: 240,
    });
    expect(create.request.body.newGuest).toBeUndefined();
    create.flush({ id: 'b1', reference: 'SD-2027-000042' });
    expect(close).toHaveBeenCalledWith({ id: 'b1', reference: 'SD-2027-000042' });
  });

  it('shows a taken email under the new guest email field', async () => {
    const dialog = await openWithStay();
    dialog.guestMode.set('new');
    dialog.newGuest.setValue({
      firstName: 'Lena',
      lastName: 'Schmidt',
      email: 'guest@example.com',
      phone: '',
    });

    dialog.submit();
    const create = http.expectOne('/api/v1/bookings');
    expect(create.request.body.newGuest).toEqual({
      firstName: 'Lena',
      lastName: 'Schmidt',
      email: 'guest@example.com',
      phone: null,
    });
    create.flush(
      {
        code: 'validation_failed',
        errors: { 'newGuest.email': ['A guest with this email already exists.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(dialog.newGuest.controls.email.getError('server')).toBe(
      'A guest with this email already exists.',
    );
    expect(close).not.toHaveBeenCalled();
  });

  it('does not book more guests than the chosen room sleeps', async () => {
    const dialog = await openWithStay();
    dialog.chooseGuest(vogel);

    dialog.stay.controls.guests.setValue(3);
    dialog.submit();

    expect(dialog.stay.controls.guests.hasError('max')).toBe(true);
    http.expectNone('/api/v1/bookings');
  });
});
