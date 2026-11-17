import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, ParamMap, Params, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Subscription, map } from 'rxjs';
import { EurPipe } from '../shared/eur.pipe';
import { Amenity, RoomType, roomTypes } from '../shared/inventory.models';
import { fromIsoDate, nightsBetween } from '../shared/iso-date';
import { LocalDatePipe } from '../shared/local-date.pipe';
import { plural } from '../shared/plural';
import {
  Paged,
  RoomSearchQuery,
  RoomSearchResult,
  RoomSearchSort,
  SortDirection,
} from './catalog.models';
import { CatalogService } from './catalog.service';
import { StaySearch, StaySearchForm } from './stay-search-form';

const sortOptions = ['price-asc', 'price-desc', 'capacity-desc', 'capacity-asc'] as const;

// The URL is the only state: filters, sort and page are read from the query string and
// every change navigates, so a search can be shared, bookmarked and survives a reload.
export function readSearchQuery(params: ParamMap): RoomSearchQuery | null {
  const city = params.get('city');
  const checkIn = params.get('checkIn') ?? '';
  const checkOut = params.get('checkOut') ?? '';
  const guests = Number(params.get('guests'));
  if (!city || !fromIsoDate(checkIn) || !fromIsoDate(checkOut) || !guests) {
    return null;
  }
  const type = params.get('type') as RoomType | null;
  const maxPrice = Number(params.get('maxPrice'));
  return {
    city,
    checkIn,
    checkOut,
    guests,
    type: type && roomTypes.includes(type) ? type : null,
    maxPrice: maxPrice > 0 ? maxPrice : null,
    amenities: params.getAll('amenities'),
    sort: params.get('sort') === 'capacity' ? 'capacity' : 'price',
    dir: params.get('dir') === 'desc' ? 'desc' : 'asc',
    page: Math.max(1, Math.floor(Number(params.get('page'))) || 1),
  };
}

// Defaults are left out, so the plain search URL stays short.
function toQueryParams(query: RoomSearchQuery): Params {
  return {
    city: query.city,
    checkIn: query.checkIn,
    checkOut: query.checkOut,
    guests: query.guests,
    type: query.type,
    maxPrice: query.maxPrice,
    amenities: query.amenities.length > 0 ? query.amenities : null,
    sort: query.sort === 'price' ? null : query.sort,
    dir: query.dir === 'asc' ? null : query.dir,
    page: query.page > 1 ? query.page : null,
  };
}

@Component({
  selector: 'sd-search-page',
  imports: [
    RouterLink,
    TranslatePipe,
    EurPipe,
    LocalDatePipe,
    StaySearchForm,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './search-page.html',
  styleUrl: './search-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchPage {
  readonly plural = plural;
  private readonly catalog = inject(CatalogService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly roomTypes = roomTypes;
  readonly sortOptions = sortOptions;
  readonly amenities = signal<Amenity[]>([]);
  readonly query = signal<RoomSearchQuery | null>(null);
  readonly result = signal<Paged<RoomSearchResult> | null>(null);
  readonly state = signal<'idle' | 'loading' | 'loaded' | 'invalid' | 'error'>('idle');

  readonly staySearch = computed<StaySearch | null>(() => {
    const query = this.query();
    return query
      ? { city: query.city, checkIn: query.checkIn, checkOut: query.checkOut, guests: query.guests }
      : null;
  });
  readonly nights = computed(() => {
    const query = this.query();
    const checkIn = fromIsoDate(query?.checkIn ?? null);
    const checkOut = fromIsoDate(query?.checkOut ?? null);
    return checkIn && checkOut ? nightsBetween(checkIn, checkOut) : 0;
  });
  readonly sort = computed(() => {
    const query = this.query();
    return query ? `${query.sort}-${query.dir}` : 'price-asc';
  });

  private request?: Subscription;

  constructor() {
    this.catalog.listAmenities().subscribe((amenities) => this.amenities.set(amenities));
    this.route.queryParamMap.pipe(map(readSearchQuery), takeUntilDestroyed()).subscribe((query) => {
      this.query.set(query);
      this.load();
    });
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  // A newer filter change cancels the request still in flight, so results never arrive out of order.
  load(): void {
    this.request?.unsubscribe();
    const query = this.query();
    if (!query) {
      this.state.set('idle');
      return;
    }
    this.state.set('loading');
    this.request = this.catalog.search(query).subscribe({
      next: (result) => {
        this.result.set(result);
        this.state.set('loaded');
      },
      error: (error: unknown) => {
        const invalid = error instanceof HttpErrorResponse && error.status === 400;
        this.state.set(invalid ? 'invalid' : 'error');
      },
    });
  }

  newSearch(search: StaySearch): void {
    const query = this.query();
    this.update({
      type: null,
      maxPrice: null,
      amenities: [],
      sort: 'price',
      dir: 'asc',
      ...query,
      ...search,
    });
  }

  amenityNames(room: RoomSearchResult): string {
    return room.amenities.map((a) => a.name).join(', ');
  }

  setType(type: RoomType | null): void {
    this.update({ type });
  }

  setMaxPrice(value: string): void {
    const maxPrice = Number(value);
    this.update({ maxPrice: maxPrice > 0 ? maxPrice : null });
  }

  toggleAmenity(id: string, checked: boolean): void {
    const current = this.query()?.amenities ?? [];
    this.update({ amenities: checked ? [...current, id] : current.filter((a) => a !== id) });
  }

  setSort(option: string): void {
    const [sort, dir] = option.split('-') as [RoomSearchSort, SortDirection];
    this.update({ sort, dir });
  }

  goToPage(page: number): void {
    this.update({ page });
  }

  // Any filter change starts again at page 1; only the pager keeps its page.
  private update(changes: Partial<RoomSearchQuery>): void {
    const query = this.query();
    const next = { ...query, ...changes, page: changes.page ?? 1 } as RoomSearchQuery;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(next),
    });
  }
}
