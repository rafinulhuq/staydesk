import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { Paged } from '../../public/catalog.models';
import { BookingSummary } from '../../shared/booking.models';
import { BookingsPage } from './bookings-page';

const becker: BookingSummary = {
  id: 'b1',
  reference: 'SD-2027-000042',
  roomId: 'r1',
  roomNumber: '12',
  roomType: 'Double',
  propertyId: 'p1',
  propertyName: 'Hotel Spreeblick',
  checkIn: '2027-03-10',
  checkOut: '2027-03-12',
  guests: 2,
  guestFullName: 'Anna Becker',
  status: 'Confirmed',
  totalPrice: 240,
  expiresAt: null,
};

function page(items: BookingSummary[], totalCount = items.length): Paged<BookingSummary> {
  return { items, page: 1, pageSize: 20, totalCount, totalPages: Math.ceil(totalCount / 20) };
}

// BO-1 and BO-3: filters, sort and page live in the URL and go to the API unchanged.
describe('BookingsPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  async function open(url: string): Promise<BookingsPage> {
    TestBed.configureTestingModule({
      providers: [
        // The date adapter is provided on the route, as in admin.routes.ts.
        provideRouter([
          {
            path: 'admin/bookings',
            component: BookingsPage,
            providers: [provideNativeDateAdapter()],
          },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url, BookingsPage);
    http.expectOne('/api/v1/properties').flush([]);
    return component;
  }

  function listRequest() {
    return http.expectOne((request) => request.url === '/api/v1/bookings');
  }

  afterEach(() => http.verify());

  it('sends the filters in the URL to the API and drops an unknown status', async () => {
    await open(
      '/admin/bookings?propertyId=p1&status=Lost&from=2027-03-01&to=2027-03-31&search=becker&sort=GuestName&dir=desc&page=2',
    );

    const params = listRequest().request.params;
    expect(params.get('propertyId')).toBe('p1');
    expect(params.has('status')).toBe(false);
    expect(params.get('from')).toBe('2027-03-01');
    expect(params.get('to')).toBe('2027-03-31');
    expect(params.get('search')).toBe('becker');
    expect(params.get('sort')).toBe('GuestName');
    expect(params.get('dir')).toBe('desc');
    expect(params.get('page')).toBe('2');
  });

  it('sorts by a clicked column header and goes back to page 1', async () => {
    await open('/admin/bookings?page=3');
    listRequest().flush(page([becker], 45));
    await harness.fixture.whenStable();

    const totalHeader = [
      ...harness.fixture.nativeElement.querySelectorAll('th .mat-sort-header-container'),
    ].find((header: HTMLElement) => header.textContent?.includes('book.total')) as HTMLElement;
    totalHeader.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/admin/bookings?sort=TotalPrice');
    const params = listRequest().request.params;
    expect(params.get('sort')).toBe('TotalPrice');
    expect(params.get('page')).toBe('1');
  });

  it('searches once the guest name has stopped changing', async () => {
    const component = await open('/admin/bookings');
    listRequest().flush(page([becker]));

    component.search.setValue('Bec');
    component.search.setValue('Becker');
    await new Promise((resolve) => setTimeout(resolve, 350));
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/admin/bookings?search=Becker');
    expect(listRequest().request.params.get('search')).toBe('Becker');
  });

  it('says to narrow the filters when the export has too many rows', async () => {
    const component = await open('/admin/bookings?status=Cancelled');
    listRequest().flush(page([becker]));

    component.export();
    const exportRequest = http.expectOne((request) => request.url === '/api/v1/bookings/export');
    expect(exportRequest.request.params.get('status')).toBe('Cancelled');
    exportRequest.flush(new Blob(), { status: 400, statusText: 'Bad Request' });
    await harness.fixture.whenStable();

    expect(harness.fixture.nativeElement.textContent).toContain(
      'admin.bookings.exportErrors.tooMany',
    );
  });

  it('opens the walk-in form with the date adapter from the route', async () => {
    const component = await open('/admin/bookings');
    listRequest().flush(page([becker]));

    component.newWalkIn();
    await harness.fixture.whenStable();

    expect(document.querySelector('sd-walk-in-dialog mat-date-range-input')).not.toBeNull();
    http.expectOne('/api/v1/properties').flush([]);
  });
});
