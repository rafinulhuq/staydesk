import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCalendarCellClassFunction, MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, filter, forkJoin, map, of, startWith, switchMap, tap } from 'rxjs';
import { EurPipe } from '../shared/eur.pipe';
import { PropertyDetail, RoomDetail } from '../shared/inventory.models';
import { addDays, fromIsoDate, toIsoDate, today } from '../shared/iso-date';
import { LocalDatePipe } from '../shared/local-date.pipe';
import { plural } from '../shared/plural';
import { StayQuote } from './catalog.models';
import { CatalogService } from './catalog.service';

const calendarDays = 60;

@Component({
  selector: 'sd-room-page',
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
  ],
  templateUrl: './room-page.html',
  styleUrl: './detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomPage {
  readonly plural = plural;
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  // The route is rooms/:id, so the parameter is always there.
  private readonly id = this.route.snapshot.paramMap.get('id')!;

  readonly today = today();
  readonly lastCalendarDay = addDays(this.today, calendarDays - 1);
  readonly room = signal<RoomDetail | null>(null);
  readonly property = signal<PropertyDetail | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'notFound' | 'error'>('loading');
  // null until loaded; the calendar only renders once it knows the booked dates.
  readonly bookedDates = signal<Set<string> | null>(null);
  readonly quote = signal<StayQuote | null>(null);
  readonly quoteState = signal<'idle' | 'loading' | 'loaded' | 'invalid' | 'error'>('idle');
  readonly nights = computed(() => this.quote()?.nights.length ?? 0);

  // Dates come from the search result link, so the quote matches what the guest searched.
  readonly stay = inject(NonNullableFormBuilder).group({
    checkIn: [fromIsoDate(this.route.snapshot.queryParamMap.get('checkIn'))],
    checkOut: [fromIsoDate(this.route.snapshot.queryParamMap.get('checkOut'))],
  });

  readonly isFree = (date: Date | null): boolean =>
    !date || !this.bookedDates()?.has(toIsoDate(date));
  // Booked dates are crossed out as well as disabled, so colour isn't the only signal.
  readonly bookedClass: MatCalendarCellClassFunction<Date> = (date, view) =>
    view === 'month' && !this.isFree(date) ? 'sd-booked-date' : '';

  constructor() {
    this.load();
    this.stay.valueChanges
      .pipe(
        startWith(this.stay.getRawValue()),
        map(() => this.stay.getRawValue()),
        filter(
          (stay): stay is { checkIn: Date; checkOut: Date } => !!stay.checkIn && !!stay.checkOut,
        ),
        tap(() => this.quoteState.set('loading')),
        switchMap(({ checkIn, checkOut }) =>
          this.catalog.quote(this.id, toIsoDate(checkIn), toIsoDate(checkOut)).pipe(
            catchError((error: unknown) => {
              const invalid = error instanceof HttpErrorResponse && error.status === 400;
              this.quoteState.set(invalid ? 'invalid' : 'error');
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
      .getRoom(this.id)
      .pipe(
        switchMap((room) =>
          forkJoin({
            room: of(room),
            property: this.catalog.getProperty(room.propertyId),
          }),
        ),
      )
      .subscribe({
        next: ({ room, property }) => {
          this.room.set(room);
          this.property.set(property);
          this.state.set('loaded');
          this.loadAvailability();
        },
        error: (error: unknown) =>
          this.state.set(
            error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error',
          ),
      });
  }

  // SRCH-5. A failure only hides the calendar; the quote still works without it.
  private loadAvailability(): void {
    this.catalog
      .availability(this.id, toIsoDate(this.today), toIsoDate(addDays(this.today, calendarDays)))
      .subscribe({
        next: (availability) => this.bookedDates.set(new Set(availability.bookedDates)),
        error: () => this.bookedDates.set(null),
      });
  }
}
