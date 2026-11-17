import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { Paged, RoomSearchResult } from './catalog.models';
import { SearchPage } from './search-page';

const stay = 'city=Berlin&checkIn=2026-12-10&checkOut=2026-12-13&guests=2';

function page(
  items: RoomSearchResult[],
  pageNumber = 1,
  totalCount = items.length,
): Paged<RoomSearchResult> {
  return {
    items,
    page: pageNumber,
    pageSize: 10,
    totalCount,
    totalPages: Math.ceil(totalCount / 10),
  };
}

// SRS §9: search filter ↔ URL sync.
describe('SearchPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  async function open(url: string): Promise<SearchPage> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'search', component: SearchPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        provideNativeDateAdapter(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url, SearchPage);
    http.expectOne('/api/v1/amenities').flush([
      { id: 'wifi', name: 'Wi-Fi', icon: 'wifi' },
      { id: 'breakfast', name: 'Breakfast', icon: 'free_breakfast' },
    ]);
    return component;
  }

  function searchRequest() {
    return http.expectOne((request) => request.url === '/api/v1/rooms/search');
  }

  afterEach(() => http.verify());

  it('sends the filters in the URL to the API', async () => {
    await open(
      `/search?${stay}&type=Suite&maxPrice=500&amenities=wifi&amenities=breakfast&sort=capacity&dir=desc&page=2`,
    );

    const params = searchRequest().request.params;
    expect(params.get('city')).toBe('Berlin');
    expect(params.get('checkIn')).toBe('2026-12-10');
    expect(params.get('checkOut')).toBe('2026-12-13');
    expect(params.get('guests')).toBe('2');
    expect(params.get('type')).toBe('Suite');
    expect(params.get('maxPrice')).toBe('500');
    expect(params.getAll('amenities')).toEqual(['wifi', 'breakfast']);
    expect(params.get('sort')).toBe('capacity');
    expect(params.get('dir')).toBe('desc');
    expect(params.get('page')).toBe('2');
  });

  it('shows the filters from the URL as selected', async () => {
    await open(`/search?${stay}&amenities=breakfast`);
    searchRequest().flush(page([]));
    await harness.fixture.whenStable();

    const checkboxes = [
      ...harness.fixture.nativeElement.querySelectorAll('mat-checkbox input'),
    ] as HTMLInputElement[];
    expect(checkboxes.map((box) => box.checked)).toEqual([false, true]);
  });

  it('writes a changed filter to the URL and goes back to page 1', async () => {
    const component = await open(`/search?${stay}&page=3`);
    searchRequest().flush(page([], 3, 25));

    component.toggleAmenity('wifi', true);
    await harness.fixture.whenStable();
    component.setMaxPrice('400');
    await harness.fixture.whenStable();

    const url = TestBed.inject(Router).url;
    expect(url).toContain('amenities=wifi');
    expect(url).toContain('maxPrice=400');
    expect(url).not.toContain('page=');
    // Each navigation searched again; the first request was cancelled by the second.
    const requests = http.match((request) => request.url === '/api/v1/rooms/search');
    expect(requests.map((r) => r.cancelled)).toEqual([true, false]);
    expect(requests[1].request.params.getAll('amenities')).toEqual(['wifi']);
  });

  it('keeps the filters when the pager moves to another page', async () => {
    const component = await open(`/search?${stay}&type=Twin`);
    searchRequest().flush(page([], 1, 25));

    component.goToPage(2);
    await harness.fixture.whenStable();

    const params = searchRequest().request.params;
    expect(params.get('type')).toBe('Twin');
    expect(params.get('page')).toBe('2');
  });

  it('says which city and dates had no free rooms', async () => {
    await open(`/search?${stay}`);
    searchRequest().flush(page([]));
    await harness.fixture.whenStable();

    expect(harness.fixture.nativeElement.textContent).toContain('search.empty');
  });
});
