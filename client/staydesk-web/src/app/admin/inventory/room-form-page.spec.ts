import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { RoomDetail } from '../../shared/inventory.models';
import { RoomFormPage } from './room-form-page';

function room(version: number, basePrice: number): RoomDetail {
  return {
    id: 'room-1',
    propertyId: 'property-1',
    roomNumber: '101',
    type: 'Double',
    maxGuests: 2,
    basePrice,
    imageUrls: [],
    amenities: [],
    isActive: true,
    version,
  };
}

describe('RoomFormPage', () => {
  let http: HttpTestingController;

  async function openRoom(): Promise<{ page: RoomFormPage; harness: RouterTestingHarness }> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'admin/rooms/:id', component: RoomFormPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/admin/rooms/room-1', RoomFormPage);
    http.expectOne('/api/v1/amenities').flush([]);
    http.expectOne('/api/v1/rooms/room-1').flush(room(7, 89));
    await harness.fixture.whenStable();
    return { page, harness };
  }

  it('sends the version it loaded, so the API can detect a later save by someone else', async () => {
    const { page } = await openRoom();

    page.form.controls.basePrice.setValue(95);
    page.submit();

    const put = http.expectOne({ method: 'PUT', url: '/api/v1/rooms/room-1' });
    expect(put.request.body).toMatchObject({ basePrice: 95, version: 7 });
  });

  it('on 409 blocks saving until the admin reloads, then saves against the new version', async () => {
    const { page, harness } = await openRoom();

    page.submit();
    http
      .expectOne({ method: 'PUT', url: '/api/v1/rooms/room-1' })
      .flush({ code: 'concurrency_conflict' }, { status: 409, statusText: 'Conflict' });
    await harness.fixture.whenStable();
    const element: HTMLElement = harness.fixture.nativeElement;

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('admin.rooms.conflict');
    expect(element.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);

    page.reload();
    http.expectOne('/api/v1/rooms/room-1').flush(room(8, 99));
    expect(page.form.controls.basePrice.value).toBe(99);

    page.submit();
    const put = http.expectOne({ method: 'PUT', url: '/api/v1/rooms/room-1' });
    expect(put.request.body).toMatchObject({ version: 8 });
  });
});
