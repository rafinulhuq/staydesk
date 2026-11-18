import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Paged } from '../../public/catalog.models';
import { BookingListQuery, BookingSummary, GuestSummary } from '../../shared/booking.models';

export const bookingListPageSize = 20;

@Injectable({ providedIn: 'root' })
export class BookingListService {
  private readonly http = inject(HttpClient);

  list(query: BookingListQuery): Observable<Paged<BookingSummary>> {
    const params = filterParams(query).set('page', query.page).set('pageSize', bookingListPageSize);
    return this.http.get<Paged<BookingSummary>>('/api/v1/bookings', { params });
  }

  // BO-3. Fetched through HttpClient rather than a plain link, so the bearer token goes along.
  exportCsv(query: BookingListQuery): Observable<Blob> {
    return this.http.get('/api/v1/bookings/export', {
      params: filterParams(query),
      responseType: 'blob',
    });
  }

  // BOOK-9: the existing guest a walk-in is for.
  searchGuests(search: string): Observable<GuestSummary[]> {
    return this.http.get<GuestSummary[]>('/api/v1/guests', { params: { search } });
  }
}

function filterParams(query: BookingListQuery): HttpParams {
  let params = new HttpParams().set('sort', query.sort).set('dir', query.dir);
  for (const [key, value] of [
    ['propertyId', query.propertyId],
    ['status', query.status],
    ['from', query.from],
    ['to', query.to],
    ['search', query.search.trim()],
  ] as const) {
    if (value) {
      params = params.set(key, value);
    }
  }
  return params;
}
