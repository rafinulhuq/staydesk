import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { ActiveBookingsDialog } from './active-bookings-dialog';
import { ActiveBookingWarning, RoomDetail } from './inventory.models';
import { RoomsPage } from './rooms-page';

function room(id: string, roomNumber: string): RoomDetail {
  return {
    id,
    propertyId: 'property-1',
    roomNumber,
    type: 'Double',
    maxGuests: 2,
    basePrice: 89,
    imageUrls: [],
    amenities: [],
    isActive: true,
    version: 1,
  };
}

describe('RoomsPage', () => {
  let http: HttpTestingController;
  // Confirm dialogs answer "yes" straight away.
  const dialog = { open: vi.fn(() => ({ afterClosed: () => of(true) })) };

  async function open(rooms: RoomDetail[]): Promise<RouterTestingHarness> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'admin/rooms', component: RoomsPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: MatDialog, useValue: dialog },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/admin/rooms?propertyId=property-1', RoomsPage);
    http
      .expectOne('/api/v1/properties?includeInactive=true')
      .flush([{ id: 'property-1', name: 'Hotel Spreeblick', city: 'Berlin', isActive: true }]);
    http.expectOne('/api/v1/properties/property-1?includeInactive=true').flush({ rooms });
    await harness.fixture.whenStable();
    return harness;
  }

  it('lists room numbers in natural order (2 before 10)', async () => {
    const harness = await open([room('a', '10'), room('b', '2'), room('c', '1')]);

    const numbers = [
      ...harness.fixture.nativeElement.querySelectorAll('td.mat-column-roomNumber'),
    ].map((cell: HTMLElement) => cell.textContent?.trim());
    expect(numbers).toEqual(['1', '2', '10']);
  });

  it('shows the bookings that still use a room after deactivating it (PROP-5)', async () => {
    const harness = await open([room('a', '101')]);
    const warning: ActiveBookingWarning = {
      bookingId: 'booking-1',
      reference: 'SD-7K2M9Q',
      checkIn: '2026-12-12',
      checkOut: '2026-12-14',
      guestFullName: 'Anna Keller',
    };

    harness.routeDebugElement!.componentInstance.deactivate(room('a', '101'));
    http
      .expectOne({ method: 'DELETE', url: '/api/v1/rooms/a' })
      .flush({ activeBookings: [warning] });
    http.expectOne('/api/v1/properties/property-1?includeInactive=true').flush({ rooms: [] });

    expect(dialog.open).toHaveBeenLastCalledWith(ActiveBookingsDialog, { data: [warning] });
  });
});
