import { RoomType } from './inventory.models';
import { QuoteNight } from './pricing.models';

export type BookingStatus =
  'Pending' | 'Confirmed' | 'CheckedIn' | 'CheckedOut' | 'Cancelled' | 'NoShow';

export interface CreateBookingRequest {
  roomId: string;
  checkIn: string;
  checkOut: string;
  guests: number;
  guestFullName: string;
  guestEmail: string;
  guestPhone: string | null;
  specialRequests: string | null;
  // PRICE-7: the total the guest saw; the API answers 409 price_changed if it moved.
  expectedTotal: number | null;
}

export interface BookingDetail {
  id: string;
  reference: string;
  roomId: string;
  roomNumber: string;
  propertyId: string;
  propertyName: string;
  checkIn: string;
  checkOut: string;
  guests: number;
  guestFullName: string;
  guestEmail: string;
  guestPhone: string | null;
  specialRequests: string | null;
  status: BookingStatus;
  totalPrice: number;
  priceBreakdown: QuoteNight[];
  cancellationFee: number | null;
  refundAmount: number | null;
  feeWaived: boolean;
  createdAt: string;
  expiresAt: string | null;
  confirmedAt: string | null;
  checkedInAt: string | null;
  checkedOutAt: string | null;
  cancelledAt: string | null;
}

export type MyBookingsScope = 'Upcoming' | 'Past';

export const bookingStatuses: readonly BookingStatus[] = [
  'Pending',
  'Confirmed',
  'CheckedIn',
  'CheckedOut',
  'Cancelled',
  'NoShow',
];

// One row of My bookings and of the back-office table (BO-1).
export interface BookingSummary {
  id: string;
  reference: string;
  roomId: string;
  roomNumber: string;
  roomType: RoomType;
  propertyId: string;
  propertyName: string;
  checkIn: string;
  checkOut: string;
  guests: number;
  guestFullName: string;
  status: BookingStatus;
  totalPrice: number;
  expiresAt: string | null;
}

export interface CancellationQuote {
  amountPaid: number;
  fee: number;
  refund: number;
  freeUntil: string;
}

export type BookingListSort = 'CheckIn' | 'TotalPrice' | 'GuestName';

// BO-1. From/To pick bookings with a night in that range, both days included.
export interface BookingListQuery {
  propertyId: string | null;
  status: BookingStatus | null;
  from: string | null;
  to: string | null;
  search: string;
  sort: BookingListSort;
  dir: 'asc' | 'desc';
  page: number;
}
