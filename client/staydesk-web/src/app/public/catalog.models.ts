import { Amenity, RoomType } from '../shared/inventory.models';

// Mirrors the API contracts in StayDesk.Application (Paging/, Rooms/).
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type RoomSearchSort = 'price' | 'capacity';
export type SortDirection = 'asc' | 'desc';

export interface RoomSearchQuery {
  city: string;
  checkIn: string;
  checkOut: string;
  guests: number;
  type: RoomType | null;
  maxPrice: number | null;
  amenities: string[];
  sort: RoomSearchSort;
  dir: SortDirection;
  page: number;
}

export interface RoomSearchResult {
  roomId: string;
  propertyId: string;
  propertyName: string;
  city: string;
  roomNumber: string;
  type: RoomType;
  maxGuests: number;
  imageUrl: string | null;
  amenities: Amenity[];
  total: number;
}

export interface RoomAvailability {
  roomId: string;
  from: string;
  to: string;
  bookedDates: string[];
}
