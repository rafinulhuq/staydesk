import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BookingDetail,
  BookingSummary,
  CancellationQuote,
  CreateBookingRequest,
  MyBookingsScope,
} from '../shared/booking.models';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);

  // BOOK-8: the same key for every retry of one booking attempt, so a double click or a
  // retried request returns the booking already made instead of a second one.
  create(request: CreateBookingRequest, idempotencyKey: string): Observable<BookingDetail> {
    return this.http.post<BookingDetail>('/api/v1/bookings', request, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }

  get(id: string): Observable<BookingDetail> {
    return this.http.get<BookingDetail>(`/api/v1/bookings/${id}`);
  }

  mine(scope: MyBookingsScope): Observable<BookingSummary[]> {
    return this.http.get<BookingSummary[]>('/api/v1/bookings/my', { params: { scope } });
  }

  pay(id: string): Observable<BookingDetail> {
    return this.http.post<BookingDetail>(`/api/v1/bookings/${id}/pay`, null);
  }

  cancellationQuote(id: string): Observable<CancellationQuote> {
    return this.http.get<CancellationQuote>(`/api/v1/bookings/${id}/cancellation-quote`);
  }

  // waiveFee: staff only (BOOK-5).
  cancel(id: string, waiveFee = false): Observable<BookingDetail> {
    return this.http.post<BookingDetail>(`/api/v1/bookings/${id}/cancel`, { waiveFee });
  }

  // BO-2 staff actions.
  checkIn(id: string): Observable<BookingDetail> {
    return this.http.post<BookingDetail>(`/api/v1/bookings/${id}/check-in`, null);
  }

  checkOut(id: string): Observable<BookingDetail> {
    return this.http.post<BookingDetail>(`/api/v1/bookings/${id}/check-out`, null);
  }

  markNoShow(id: string): Observable<BookingDetail> {
    return this.http.post<BookingDetail>(`/api/v1/bookings/${id}/no-show`, null);
  }

  updateNotes(id: string, internalNotes: string): Observable<BookingDetail> {
    return this.http.patch<BookingDetail>(`/api/v1/bookings/${id}/notes`, { internalNotes });
  }
}
