import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe } from '@ngx-translate/core';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  filter,
  map,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { BookingService } from '../../account/booking.service';
import { CatalogService } from '../../public/catalog.service';
import { BookingDetail, CreateBookingRequest, GuestSummary } from '../../shared/booking.models';
import { EurPipe } from '../../shared/eur.pipe';
import { PropertySummary, RoomDetail } from '../../shared/inventory.models';
import { toIsoDate, today } from '../../shared/iso-date';
import { plural } from '../../shared/plural';
import { StayQuote } from '../../shared/pricing.models';
import { applyServerErrors, problemCode } from '../../shared/server-errors';
import { BookingListService } from './booking-list.service';

// "2" before "10": the API orders room numbers as text (T-034).
const roomNumberOrder = new Intl.Collator(undefined, { numeric: true });

type Notice = 'priceChanged' | 'roomUnavailable' | 'invalidDates' | 'failed' | null;

// BOOK-9: staff book a room for an existing or a new guest. The booking is Confirmed at
// once and paid at the desk, so there is no hold or payment step. Closes with the booking.
@Component({
  selector: 'sd-walk-in-dialog',
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    EurPipe,
    MatAutocompleteModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './walk-in-dialog.html',
  styleUrl: './walk-in-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WalkInDialog {
  readonly plural = plural;
  private readonly catalog = inject(CatalogService);
  private readonly bookingList = inject(BookingListService);
  private readonly bookings = inject(BookingService);
  private readonly dialogRef = inject<MatDialogRef<WalkInDialog, BookingDetail>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);
  // One key per opened form, so a double click or a retry books once (BOOK-8).
  private readonly idempotencyKey = crypto.randomUUID();

  readonly today = today();
  readonly properties = signal<PropertySummary[]>([]);
  readonly rooms = signal<RoomDetail[]>([]);
  readonly guestMode = signal<'existing' | 'new'>('existing');
  readonly guestMatches = signal<GuestSummary[] | null>(null);
  readonly selectedGuest = signal<GuestSummary | null>(null);
  readonly quote = signal<StayQuote | null>(null);
  readonly notice = signal<Notice>(null);
  readonly busy = signal(false);

  readonly stay = this.fb.group({
    propertyId: ['', Validators.required],
    roomId: ['', Validators.required],
    checkIn: [null as Date | null, Validators.required],
    checkOut: [null as Date | null, Validators.required],
    guests: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
  });
  // Typed text while searching; the chosen guest is kept in selectedGuest.
  readonly guestSearch = this.fb.control('');
  readonly newGuest = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phone: ['', Validators.maxLength(30)],
  });
  readonly specialRequests = this.fb.control('', Validators.maxLength(500));

  readonly maxGuests = computed(
    () => this.rooms().find((r) => r.id === this.selectedRoomId())?.maxGuests ?? 10,
  );
  private readonly selectedRoomId = signal('');

  constructor() {
    this.catalog.listProperties().subscribe((properties) => this.properties.set(properties));

    this.stay.controls.propertyId.valueChanges
      .pipe(
        tap(() => {
          this.stay.controls.roomId.setValue('');
          this.rooms.set([]);
        }),
        filter((id) => !!id),
        switchMap((id) => this.catalog.getProperty(id)),
        takeUntilDestroyed(),
      )
      .subscribe((property) =>
        this.rooms.set(
          [...property.rooms].sort((a, b) => roomNumberOrder.compare(a.roomNumber, b.roomNumber)),
        ),
      );

    // The server checks the room's capacity too; this shows it before submitting.
    this.stay.controls.roomId.valueChanges.pipe(takeUntilDestroyed()).subscribe((id) => {
      this.selectedRoomId.set(id);
      const guests = this.stay.controls.guests;
      guests.setValidators([
        Validators.required,
        Validators.min(1),
        Validators.max(this.maxGuests()),
      ]);
      guests.updateValueAndValidity({ emitEvent: false });
    });

    this.stay.valueChanges
      .pipe(
        map(() => this.stay.getRawValue()),
        distinctUntilChanged(
          (a, b) => a.roomId === b.roomId && a.checkIn === b.checkIn && a.checkOut === b.checkOut,
        ),
        tap(() => this.quote.set(null)),
        filter(
          (s): s is typeof s & { checkIn: Date; checkOut: Date } =>
            !!s.roomId && !!s.checkIn && !!s.checkOut,
        ),
        switchMap((s) =>
          this.catalog.quote(s.roomId, toIsoDate(s.checkIn), toIsoDate(s.checkOut)).pipe(
            tap(() => this.notice.set(null)),
            catchError((error: unknown) => {
              this.notice.set(
                error instanceof HttpErrorResponse && error.status === 400
                  ? 'invalidDates'
                  : 'failed',
              );
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((quote) => this.quote.set(quote));

    this.guestSearch.valueChanges
      .pipe(
        tap(() => this.selectedGuest.set(null)),
        map((text) => text.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((text) =>
          text.length < 2
            ? of(null)
            : this.bookingList.searchGuests(text).pipe(catchError(() => of(null))),
        ),
        startWith(null),
        takeUntilDestroyed(),
      )
      .subscribe((matches) => this.guestMatches.set(matches));
  }

  guestLabel(guest: GuestSummary): string {
    return `${guest.firstName} ${guest.lastName}`;
  }

  chooseGuest(guest: GuestSummary): void {
    this.guestSearch.setValue(`${this.guestLabel(guest)} (${guest.email})`, { emitEvent: false });
    this.selectedGuest.set(guest);
  }

  submit(): void {
    const stay = this.stay.getRawValue();
    const quote = this.quote();
    const guest = this.guestMode() === 'existing' ? this.selectedGuest() : null;
    const newGuest = this.newGuest.getRawValue();
    const guestMissing = this.guestMode() === 'existing' ? !guest : this.newGuest.invalid;
    if (this.stay.invalid || guestMissing || !quote || !stay.checkIn || !stay.checkOut) {
      this.stay.markAllAsTouched();
      this.newGuest.markAllAsTouched();
      this.guestSearch.markAsTouched();
      return;
    }

    // The booking's guest details are the chosen guest's own (T-059).
    const request: CreateBookingRequest = {
      roomId: stay.roomId,
      checkIn: toIsoDate(stay.checkIn),
      checkOut: toIsoDate(stay.checkOut),
      guests: stay.guests,
      guestFullName: guest
        ? this.guestLabel(guest)
        : `${newGuest.firstName.trim()} ${newGuest.lastName.trim()}`,
      guestEmail: guest ? guest.email : newGuest.email,
      guestPhone: (guest ? guest.phone : newGuest.phone) || null,
      specialRequests: this.specialRequests.value || null,
      expectedTotal: quote.total,
      ...(guest
        ? { guestId: guest.id }
        : { newGuest: { ...newGuest, phone: newGuest.phone || null } }),
    };

    this.busy.set(true);
    this.notice.set(null);
    this.bookings.create(request, this.idempotencyKey).subscribe({
      next: (booking) => this.dialogRef.close(booking),
      error: (error: unknown) => {
        this.busy.set(false);
        this.handleError(error);
      },
    });
  }

  private handleError(error: unknown): void {
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
    const shown =
      applyServerErrors(this.newGuest, error, 'newGuest.') || applyServerErrors(this.stay, error);
    this.notice.set(shown ? null : 'failed');
  }
}
