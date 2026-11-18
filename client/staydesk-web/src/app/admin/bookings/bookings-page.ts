import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Injector,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription, debounceTime, distinctUntilChanged, map } from 'rxjs';
import { Paged } from '../../public/catalog.models';
import { CatalogService } from '../../public/catalog.service';
import {
  BookingListQuery,
  BookingListSort,
  BookingStatus,
  BookingSummary,
  bookingStatuses,
} from '../../shared/booking.models';
import { EurPipe } from '../../shared/eur.pipe';
import { PropertySummary } from '../../shared/inventory.models';
import { fromIsoDate, toIsoDate } from '../../shared/iso-date';
import { LocalDatePipe } from '../../shared/local-date.pipe';
import { plural } from '../../shared/plural';
import { BookingDrawer } from './booking-drawer';
import { BookingListService } from './booking-list.service';
import { WalkInDialog } from './walk-in-dialog';

// Table column ids that sort, and the API sort each one asks for.
const sortColumns: Record<string, BookingListSort> = {
  guestName: 'GuestName',
  checkIn: 'CheckIn',
  totalPrice: 'TotalPrice',
};

// As on the search page, the URL is the only state, so a filtered list can be linked.
export function readBookingListQuery(params: ParamMap): BookingListQuery {
  const status = params.get('status') as BookingStatus | null;
  const sort = params.get('sort') as BookingListSort | null;
  return {
    propertyId: params.get('propertyId'),
    status: status && bookingStatuses.includes(status) ? status : null,
    from: fromIsoDate(params.get('from')) ? params.get('from') : null,
    to: fromIsoDate(params.get('to')) ? params.get('to') : null,
    search: params.get('search') ?? '',
    sort: sort && Object.values(sortColumns).includes(sort) ? sort : 'CheckIn',
    dir: params.get('dir') === 'desc' ? 'desc' : 'asc',
    page: Math.max(1, Math.floor(Number(params.get('page'))) || 1),
  };
}

function toQueryParams(query: BookingListQuery): Params {
  return {
    propertyId: query.propertyId,
    status: query.status,
    from: query.from,
    to: query.to,
    search: query.search || null,
    sort: query.sort === 'CheckIn' ? null : query.sort,
    dir: query.dir === 'asc' ? null : query.dir,
    page: query.page > 1 ? query.page : null,
  };
}

@Component({
  selector: 'sd-bookings-page',
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    EurPipe,
    LocalDatePipe,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
  ],
  templateUrl: './bookings-page.html',
  styleUrls: ['../inventory/inventory-page.scss', './bookings-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingsPage {
  readonly plural = plural;
  private readonly bookings = inject(BookingListService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly injector = inject(Injector);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly statuses = bookingStatuses;
  readonly columns = [
    'reference',
    'guestName',
    'room',
    'checkIn',
    'checkOut',
    'status',
    'totalPrice',
  ];
  readonly properties = signal<PropertySummary[]>([]);
  readonly query = signal<BookingListQuery>(
    readBookingListQuery(this.route.snapshot.queryParamMap),
  );
  readonly result = signal<Paged<BookingSummary> | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly exportState = signal<'idle' | 'running' | 'tooMany' | 'failed'>('idle');
  readonly search = new FormControl(this.query().search, { nonNullable: true });

  readonly fromDate = computed(() => fromIsoDate(this.query().from));
  readonly toDate = computed(() => fromIsoDate(this.query().to));
  readonly sortActive = computed(
    () =>
      Object.keys(sortColumns).find((column) => sortColumns[column] === this.query().sort) ?? '',
  );

  private request?: Subscription;

  constructor() {
    // Staff can't ask for inactive properties; their bookings still show without a filter.
    inject(CatalogService)
      .listProperties()
      .subscribe((properties) => this.properties.set(properties));

    this.route.queryParamMap
      .pipe(map(readBookingListQuery), takeUntilDestroyed())
      .subscribe((query) => {
        this.query.set(query);
        // Back/forward changes the URL, not the field, so the field follows the URL.
        this.search.setValue(query.search, { emitEvent: false });
        this.load();
      });
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((search) => this.update({ search: search.trim() }));
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  // A newer filter change cancels the request still in flight, so results never arrive out of order.
  load(): void {
    this.request?.unsubscribe();
    this.state.set('loading');
    this.request = this.bookings.list(this.query()).subscribe({
      next: (result) => {
        this.result.set(result);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  setProperty(propertyId: string | null): void {
    this.update({ propertyId });
  }

  setStatus(status: BookingStatus | null): void {
    this.update({ status });
  }

  setFrom(date: Date | null): void {
    this.update({ from: date ? toIsoDate(date) : null });
  }

  setTo(date: Date | null): void {
    this.update({ to: date ? toIsoDate(date) : null });
  }

  // A third click on a header clears the sort, which means the default order.
  setSort(sort: Sort): void {
    this.update(
      sort.direction
        ? { sort: sortColumns[sort.active], dir: sort.direction }
        : { sort: 'CheckIn', dir: 'asc' },
    );
  }

  goToPage(page: number): void {
    this.update({ page });
  }

  open(booking: BookingSummary): void {
    this.dialog.open(BookingDrawer, {
      data: booking.id,
      position: { right: '0', top: '0' },
      height: '100%',
      width: '440px',
      maxWidth: '100vw',
      autoFocus: 'dialog',
    });
  }

  // BOOK-9. The list reloads, so the new Confirmed booking shows if it matches the filters.
  newWalkIn(): void {
    this.dialog
      // A dialog uses the root injector unless told otherwise; this one needs the route's
      // DateAdapter (provideLocalizedDateAdapter in admin.routes.ts).
      .open(WalkInDialog, {
        width: '640px',
        maxWidth: '100vw',
        autoFocus: 'first-tabbable',
        injector: this.injector,
      })
      .afterClosed()
      .subscribe((booking) => {
        if (booking) {
          this.snackBar.open(
            this.translate.instant('admin.walkIn.created', { reference: booking.reference }),
            this.translate.instant('common.close'),
            { duration: 6000 },
          );
          this.load();
        }
      });
  }

  export(): void {
    this.exportState.set('running');
    this.bookings.exportCsv(this.query()).subscribe({
      next: (csv) => {
        const link = document.createElement('a');
        link.href = URL.createObjectURL(csv);
        link.download = 'bookings.csv';
        link.click();
        URL.revokeObjectURL(link.href);
        this.exportState.set('idle');
      },
      // 400 here means more rows match than one export allows (BO-3).
      error: (error: unknown) =>
        this.exportState.set(
          error instanceof HttpErrorResponse && error.status === 400 ? 'tooMany' : 'failed',
        ),
    });
  }

  // Any filter change starts again at page 1; only the pager keeps its page.
  private update(changes: Partial<BookingListQuery>): void {
    const next = { ...this.query(), ...changes, page: changes.page ?? 1 };
    void this.router.navigate([], { relativeTo: this.route, queryParams: toQueryParams(next) });
  }
}
