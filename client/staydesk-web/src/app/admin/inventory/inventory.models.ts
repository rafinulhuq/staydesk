// Mirrors the API contracts in StayDesk.Application (Properties/, Rooms/).
export type RoomType = 'Single' | 'Double' | 'Twin' | 'Suite' | 'Apartment';

export const roomTypes: readonly RoomType[] = ['Single', 'Double', 'Twin', 'Suite', 'Apartment'];

export interface PropertySummary {
  id: string;
  name: string;
  city: string;
  address: string;
  imageUrl: string | null;
  isActive: boolean;
}

export interface PropertyRequest {
  name: string;
  city: string;
  address: string;
  description: string;
  imageUrl: string | null;
  checkInTime: string;
  checkOutTime: string;
  timeZone: string;
}

export interface PropertyDetail extends PropertyRequest {
  id: string;
  isActive: boolean;
  rooms: RoomDetail[];
}

export interface Amenity {
  id: string;
  name: string;
  icon: string;
}

interface RoomFields {
  roomNumber: string;
  type: RoomType;
  maxGuests: number;
  basePrice: number;
  imageUrls: string[];
  amenityIds: string[];
}

export interface CreateRoomRequest extends RoomFields {
  propertyId: string;
}

export interface UpdateRoomRequest extends RoomFields {
  version: number;
}

export interface RoomDetail {
  id: string;
  propertyId: string;
  roomNumber: string;
  type: RoomType;
  maxGuests: number;
  basePrice: number;
  imageUrls: string[];
  amenities: Amenity[];
  isActive: boolean;
  version: number;
}

export interface ActiveBookingWarning {
  bookingId: string;
  reference: string;
  checkIn: string;
  checkOut: string;
  guestFullName: string;
}

export interface RoomDeactivation {
  activeBookings: ActiveBookingWarning[];
}
