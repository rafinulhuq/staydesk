import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Amenity,
  CreateRoomRequest,
  PropertyDetail,
  PropertyRequest,
  PropertySummary,
  RoomDeactivation,
  RoomDetail,
  UpdateRoomRequest,
} from '../../shared/inventory.models';

// Admin calls always ask for inactive rows too (PROP-3), so they can be edited and reactivated.
@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly http = inject(HttpClient);

  listProperties(): Observable<PropertySummary[]> {
    return this.http.get<PropertySummary[]>('/api/v1/properties', {
      params: { includeInactive: true },
    });
  }

  getProperty(id: string): Observable<PropertyDetail> {
    return this.http.get<PropertyDetail>(`/api/v1/properties/${id}`, {
      params: { includeInactive: true },
    });
  }

  createProperty(request: PropertyRequest): Observable<PropertyDetail> {
    return this.http.post<PropertyDetail>('/api/v1/properties', request);
  }

  updateProperty(id: string, request: PropertyRequest): Observable<PropertyDetail> {
    return this.http.put<PropertyDetail>(`/api/v1/properties/${id}`, request);
  }

  deactivateProperty(id: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/properties/${id}`);
  }

  reactivateProperty(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/properties/${id}/reactivate`, null);
  }

  getRoom(id: string): Observable<RoomDetail> {
    return this.http.get<RoomDetail>(`/api/v1/rooms/${id}`);
  }

  createRoom(request: CreateRoomRequest): Observable<RoomDetail> {
    return this.http.post<RoomDetail>('/api/v1/rooms', request);
  }

  updateRoom(id: string, request: UpdateRoomRequest): Observable<RoomDetail> {
    return this.http.put<RoomDetail>(`/api/v1/rooms/${id}`, request);
  }

  deactivateRoom(id: string): Observable<RoomDeactivation> {
    return this.http.delete<RoomDeactivation>(`/api/v1/rooms/${id}`);
  }

  reactivateRoom(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/rooms/${id}/reactivate`, null);
  }

  listAmenities(): Observable<Amenity[]> {
    return this.http.get<Amenity[]>('/api/v1/amenities');
  }
}
