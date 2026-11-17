import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, filter, map, of, startWith, switchMap, tap, timer } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { CatalogService } from '../public/catalog.service';
import { BookingDetail } from '../shared/booking.models';
import { EurPipe } from '../shared/eur.pipe';
import { RoomDetail, PropertyDetail } from '../shared/inventory.models';
import { fromIsoDate, toIsoDate, today } from '../shared/iso-date';
import { LocalDatePipe } from '../shared/local-date.pipe';
import { plural } from '../shared/plural';
import { StayQuote } from '../shared/pricing.models';
import { applyServerErrors, problemCode } from '../shared/server-errors';
import { BookingService } from './booking.service';

type Notice = 'priceChanged' | 'roomUnavailable' | 'expired' | 'failed' | null;

// SRS §7 /book/:roomId: review stay → guest details → mock payment with a 15-minute countdown.
@Component({
  selector: 'sd-book-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    EurPipe,
    LocalDatePipe,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatStepperModule,
  ],
  templateUrl: './book-page.html',
  styleUrls: ['../public/detail-page.scss', './book-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookPage {
  readonly plural = plural;
  private readonly catalog = inject(CatalogService);
  private readonly bookings = inject(BookingService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly stepper = viewChild(MatStepper);
  // The route is book/:roomId, so the parameter is always there.
  private readonly roomId = this.route.snapshot.paramMap.get('roomId')!;

  readonly today = today();
  readonly room = signal<RoomDetail | null>(null);
  readonly property = signal<PropertyDetail | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'notFound' | 'error'>('loading');
  readonly quote = signal<StayQuote | null>(null);
  readonly quoteState = signal<'idle' | 'loading' | 'loaded' | 'invalid' | 'error'>('idle');
  readonly booking = signal<BookingDetail | null>(null);
  readonly notice = signal<Notice>(null);
  readonly busy = signal(false);
  private readonly now = signal(Date.now());
  private idempotencyKey = crypto.randomUUID();

  // Seconds left on the 15-minute hold; the server decides, this only shows it.
  readonly secondsLeft = computed(() => {
    const expiresAt = this.booking()?.expiresAt;
    return expiresAt ? Math.max(0, Math.floor((Date.parse(expiresAt) - this.now()) / 1000)) : 0;
  });
  readonly countdown = computed(() => {
    const s = this.secondsLeft();
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  });

  private readonly fb = inject(NonNullableFormBuilder);
  readonly stay = this.fb.group({
    checkIn: [fromIsoDate(this.route.snapshot.queryParamMap.get('checkIn')), Validators.required],
    checkOut: [fromIsoDate(this.route.snapshot.queryParamMap.get('checkOut')), Validators.required],
    guests: [
      Number(this.route.snapshot.queryParamMap.get('guests')) || 1,
      [Validators.required, Validators.min(1)],
    ],
  });

  // BOOK-1: pre-filled from the profile; the guest may book for someone else's name.
  private readonly user = inject(AuthService).user();
  readonly details = this.fb.group({
    guestFullName: [
      this.user ? `${this.user.firstName} ${this.user.lastName}` : '',
      [Validators.required, Validators.maxLength(200)],
    ],
    guestEmail: [
      this.user?.email ?? '',
      [Validators.required, Validators.email, Validators.maxLength(256)],
    ],
    guestPhone: ['', Validators.maxLength(30)],
    specialRequests: ['', Validators.maxLength(500)],
  });

  constructor() {
    this.load();
    timer(0, 1000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.now.set(Date.now());
        if (this.booking()?.status === 'Pending' && this.secondsLeft() === 0) {
          this.notice.set('expired');
        }
      });
    this.stay.valueChanges
      .pipe(
        startWith(null),
        map(() => this.stay.getRawValue()),
        filter(
          (s): s is { checkIn: Date; checkOut: Date; guests: number } =>
            !!s.checkIn && !!s.checkOut,
        ),
        tap(() => this.quoteState.set('loading')),
        switchMap(({ checkIn, checkOut }) =>
          this.catalog.quote(this.roomId, toIsoDate(checkIn), toIsoDate(checkOut)).pipe(
            catchError((error: unknown) => {
              this.quoteState.set(
                error instanceof HttpErrorResponse && error.status === 400 ? 'invalid' : 'error',
              );
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((quote) => {
        this.quote.set(quote);
        if (quote) {
          this.quoteState.set('loaded');
        }
      });
  }

  load(): void {
    this.state.set('loading');
    this.catalog
      .getRoom(this.roomId)
      .pipe(
        switchMap((room) =>
          this.catalog.getProperty(room.propertyId).pipe(map((property) => ({ room, property }))),
        ),
      )
      .subscribe({
        next: ({ room, property }) => {
          this.room.set(room);
          this.property.set(property);
          this.stay.controls.guests.addValidators(Validators.max(room.maxGuests));
          // Re-validates without a value event, so the quote isn't requested a second time.
          this.stay.controls.guests.updateValueAndValidity({ emitEvent: false });
          this.state.set('loaded');
        },
        error: (error: unknown) =>
          this.state.set(
            error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error',
          ),
      });
  }

  // Step 2 → 3: creates the Pending booking that holds the room for 15 minutes.
  reserve(): void {
    const { checkIn, checkOut, guests } = this.stay.getRawValue();
    const quote = this.quote();
    if (this.details.invalid || this.stay.invalid || !checkIn || !checkOut || !quote) {
      this.details.markAllAsTouched();
      return;
    }

    const details = this.details.getRawValue();
    this.busy.set(true);
    this.notice.set(null);
    this.bookings
      .create(
        {
          roomId: this.roomId,
          checkIn: toIsoDate(checkIn),
          checkOut: toIsoDate(checkOut),
          guests,
          guestFullName: details.guestFullName,
          guestEmail: details.guestEmail,
          guestPhone: details.guestPhone || null,
          specialRequests: details.specialRequests || null,
          expectedTotal: quote.total,
        },
        this.idempotencyKey,
      )
      .subscribe({
        next: (booking) => {
          this.busy.set(false);
          this.booking.set(booking);
          this.stepper()?.next();
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.handleReserveError(error);
        },
      });
  }

  pay(): void {
    const booking = this.booking();
    if (!booking) {
      return;
    }
    this.busy.set(true);
    this.bookings.pay(booking.id).subscribe({
      next: () => void this.router.navigate(['/book/confirmation', booking.id]),
      error: (error: unknown) => {
        this.busy.set(false);
        this.notice.set(problemCode(error) === 'booking_expired' ? 'expired' : 'failed');
      },
    });
  }

  // After an expired hold: a new attempt needs a new key, or the API would return the old booking.
  startAgain(): void {
    this.booking.set(null);
    this.notice.set(null);
    this.idempotencyKey = crypto.randomUUID();
    const stepper = this.stepper();
    if (stepper) {
      stepper.selectedIndex = 0;
    }
  }

  private handleReserveError(error: unknown): void {
    switch (problemCode(error)) {
      // PRICE-7: show the new price; booking again sends it as the expected total.
      case 'price_changed':
        this.quote.set((error as HttpErrorResponse).error.quote as StayQuote);
        this.notice.set('priceChanged');
        return;
      case 'room_unavailable':
        this.notice.set('roomUnavailable');
        return;
    }
    const shown = applyServerErrors(this.details, error) || applyServerErrors(this.stay, error);
    this.notice.set(shown ? null : 'failed');
  }
}
