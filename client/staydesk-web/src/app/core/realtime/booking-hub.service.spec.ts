import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { BookingEvent, BookingHubService } from './booking-hub.service';

// A stand-in for one SignalR connection: records what the service asks of it and lets the
// test raise server events and a reconnect.
const hub = vi.hoisted(() => {
  const state = {
    handlers: new Map<string, (payload: unknown) => void>(),
    reconnected: [] as (() => void)[],
    options: undefined as { accessTokenFactory: () => Promise<string> } | undefined,
    connection: {
      on: (name: string, handler: (payload: unknown) => void) => state.handlers.set(name, handler),
      onreconnected: (handler: () => void) => state.reconnected.push(handler),
      start: vi.fn(() => Promise.resolve()),
      stop: vi.fn(() => Promise.resolve()),
      invoke: vi.fn(() => Promise.resolve()),
    },
  };
  return state;
});

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl(_url: string, options: typeof hub.options) {
      hub.options = options;
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return hub.connection;
    }
  },
}));

const settle = () => new Promise((resolve) => setTimeout(resolve));

// SRS §7 BookingHubService and CAL-2.
describe('BookingHubService', () => {
  let service: BookingHubService;
  const freshAccessToken = vi.fn(() => Promise.resolve('token-1'));

  beforeEach(() => {
    hub.handlers.clear();
    hub.reconnected.length = 0;
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { freshAccessToken } }],
    });
    service = TestBed.inject(BookingHubService);
  });

  it('connects with a fresh token on the first watcher, joins the property and passes on only its events', async () => {
    const received: BookingEvent[] = [];
    service.watchProperty('p1').subscribe((event) => received.push(event));
    await settle();

    expect(hub.connection.start).toHaveBeenCalledTimes(1);
    expect(await hub.options!.accessTokenFactory()).toBe('token-1');
    expect(hub.connection.invoke).toHaveBeenCalledWith('WatchProperty', 'p1');

    hub.handlers.get('BookingCreated')!({ bookingId: 'b1', propertyId: 'p1', status: 'Pending' });
    hub.handlers.get('BookingCancelled')!({
      bookingId: 'b2',
      propertyId: 'p2',
      status: 'Cancelled',
    });

    expect(received).toEqual([
      { type: 'BookingCreated', bookingId: 'b1', propertyId: 'p1', status: 'Pending' },
    ]);
  });

  it('joins every watched group again after a reconnect', async () => {
    service.watchProperty('p1').subscribe();
    service.watchDashboard().subscribe();
    await settle();
    hub.connection.invoke.mockClear();

    hub.reconnected.forEach((handler) => handler());

    expect(hub.connection.invoke).toHaveBeenCalledWith('WatchProperty', 'p1');
    expect(hub.connection.invoke).toHaveBeenCalledWith('WatchDashboard');
  });

  it('leaves a group when its last watcher goes and closes the connection when nobody watches', async () => {
    const first = service.watchProperty('p1').subscribe();
    const second = service.watchProperty('p1').subscribe();
    const dashboard = service.watchDashboard().subscribe();
    await settle();
    expect(hub.connection.start).toHaveBeenCalledTimes(1);

    first.unsubscribe();
    expect(hub.connection.invoke).not.toHaveBeenCalledWith('UnwatchProperty', 'p1');
    second.unsubscribe();
    expect(hub.connection.invoke).toHaveBeenCalledWith('UnwatchProperty', 'p1');
    expect(hub.connection.stop).not.toHaveBeenCalled();

    dashboard.unsubscribe();
    expect(hub.connection.stop).toHaveBeenCalledTimes(1);
  });
});
