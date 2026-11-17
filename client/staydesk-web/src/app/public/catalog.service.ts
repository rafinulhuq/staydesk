import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Amenity, PropertyDetail, PropertySummary, RoomDetail } from '../shared/inventory.models';
import {
  Paged,
  RoomAvailability,
  RoomSearchQuery,
  RoomSearchResult,
  StayQuote,
} from './catalog.models';

export const searchPageSize = 10;

// Public reads: inactive properties and rooms never come back (PROP-3).
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);

  search(query: RoomSearchQuery): Observable<Paged<RoomSearchResult>> {
    let params = new HttpParams()
      .set('city', query.city)
      .set('checkIn', query.checkIn)
      .set('checkOut', query.checkOut)
      .set('guests', query.guests)
      .set('sort', query.sort)
      .set('dir', query.dir)
      .set('page', query.page)
      .set('pageSize', searchPageSize);
    if (query.type) {
      params = params.set('type', query.type);
    }
    if (query.maxPrice !== null) {
      params = params.set('maxPrice', query.maxPrice);
    }
    for (const id of query.amenities) {
      params = params.append('amenities', id);
    }
    return this.http.get<Paged<RoomSearchResult>>('/api/v1/rooms/search', { params });
  }

  listProperties(): Observable<PropertySummary[]> {
    return this.http.get<PropertySummary[]>('/api/v1/properties');
  }

  getProperty(id: string): Observable<PropertyDetail> {
    return this.http.get<PropertyDetail>(`/api/v1/properties/${id}`);
  }

  getRoom(id: string): Observable<RoomDetail> {
    return this.http.get<RoomDetail>(`/api/v1/rooms/${id}`);
  }

  listAmenities(): Observable<Amenity[]> {
    return this.http.get<Amenity[]>('/api/v1/amenities');
  }

  availability(roomId: string, from: string, to: string): Observable<RoomAvailability> {
    return this.http.get<RoomAvailability>(`/api/v1/rooms/${roomId}/availability`, {
      params: { from, to },
    });
  }

  quote(roomId: string, checkIn: string, checkOut: string): Observable<StayQuote> {
    return this.http.post<StayQuote>('/api/v1/pricing/quote', { roomId, checkIn, checkOut });
  }
}
