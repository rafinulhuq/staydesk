import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Observable, Subject, filter } from 'rxjs';
import { BookingStatus } from '../../shared/booking.models';
import { AuthService } from '../auth/auth.service';

export const bookingEventTypes = ['BookingCreated', 'BookingUpdated', 'BookingCancelled'] as const;
export type BookingEventType = (typeof bookingEventTypes)[number];

// SRS §6 /hubs/bookings payload, plus which event it was.
export interface BookingEvent {
  type: BookingEventType;
  bookingId: string;
  propertyId: string;
  roomId: string;
  checkIn: string;
  checkOut: string;
  status: BookingStatus;
}

interface Watch {
  count: number;
  join: (connection: HubConnection) => Promise<void>;
  leave: (connection: HubConnection) => Promise<void>;
}

const retryStartAfterMs = 5000;

// SRS §7: wraps the SignalR connection and hands out booking events as observables. The
// connection opens with the first watcher and closes with the last. Every connect and
// reconnect asks AuthService for a token that is still valid, and SignalR forgets groups on
// reconnect, so every watched group is joined again.
@Injectable({ providedIn: 'root' })
export class BookingHubService {
  private readonly auth = inject(AuthService);
  private readonly events = new Subject<BookingEvent>();
  private readonly watches = new Map<string, Watch>();
  private connection: HubConnection | null = null;
  private ready: Promise<HubConnection> | null = null;

  // CAL-2: events for one property's calendar.
  watchProperty(propertyId: string): Observable<BookingEvent> {
    return this.watch(
      `property-${propertyId}`,
      (c) => c.invoke('WatchProperty', propertyId),
      (c) => c.invoke('UnwatchProperty', propertyId),
    ).pipe(filter((event) => event.propertyId === propertyId));
  }

  // CAL-2: events for every property, for the dashboard.
  watchDashboard(): Observable<BookingEvent> {
    return this.watch(
      'dashboard',
      (c) => c.invoke('WatchDashboard'),
      (c) => c.invoke('UnwatchDashboard'),
    );
  }

  private watch(key: string, join: Watch['join'], leave: Watch['leave']): Observable<BookingEvent> {
    return new Observable<BookingEvent>((subscriber) => {
      const subscription = this.events.subscribe(subscriber);
      const existing = this.watches.get(key);
      if (existing) {
        existing.count++;
      } else {
        this.watches.set(key, { count: 1, join, leave });
        // A failed join or a start given up after the last watcher left only means no live
        // updates; the page still has its data.
        this.connected()
          .then((connection) => join(connection))
          .catch(() => undefined);
      }

      return () => {
        subscription.unsubscribe();
        const watch = this.watches.get(key);
        if (watch && --watch.count === 0) {
          this.watches.delete(key);
          if (this.watches.size === 0) {
            this.disconnect();
          } else if (this.connection) {
            void watch.leave(this.connection).catch(() => undefined);
          }
        }
      };
    });
  }

  private connected(): Promise<HubConnection> {
    this.ready ??= this.open();
    return this.ready;
  }

  private async open(): Promise<HubConnection> {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/bookings', {
        accessTokenFactory: () => this.auth.freshAccessToken(),
        // Bearer tokens, no cookies, so a cross-origin API needs no credentialed CORS.
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();
    for (const type of bookingEventTypes) {
      connection.on(type, (payload: Omit<BookingEvent, 'type'>) =>
        this.events.next({ type, ...payload }),
      );
    }
    connection.onreconnected(() => {
      for (const watch of this.watches.values()) {
        void watch.join(connection).catch(() => undefined);
      }
    });
    this.connection = connection;

    // withAutomaticReconnect only covers a connection that was up once; a failed first start
    // (API restarting) is retried while anyone still watches.
    for (;;) {
      try {
        await connection.start();
        break;
      } catch (error) {
        if (this.connection !== connection) {
          throw error;
        }
        await new Promise((resolve) => setTimeout(resolve, retryStartAfterMs));
      }
    }
    if (this.connection !== connection) {
      await connection.stop();
      throw new Error('Nobody watches any more');
    }
    return connection;
  }

  private disconnect(): void {
    const connection = this.connection;
    this.connection = null;
    this.ready = null;
    void connection?.stop();
  }
}
