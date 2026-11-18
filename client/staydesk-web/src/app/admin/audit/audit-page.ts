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
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, ParamMap, Params, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Subscription, map } from 'rxjs';
import { Paged } from '../../public/catalog.models';
import { AuditEntry, AuditLogQuery, auditedEntities } from '../../shared/audit.models';
import { fromIsoDate, toIsoDate } from '../../shared/iso-date';
import { LocalDatePipe } from '../../shared/local-date.pipe';
import { AuditChanges } from './audit-changes';
import { AuditService } from './audit.service';

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// The URL is the only state, as on the bookings page.
export function readAuditQuery(params: ParamMap): AuditLogQuery {
  const entity = params.get('entity');
  const id = (name: string) => (guid.test(params.get(name) ?? '') ? params.get(name) : null);
  return {
    entity: entity && (auditedEntities as readonly string[]).includes(entity) ? entity : null,
    entityId: id('entityId'),
    userId: id('userId'),
    from: fromIsoDate(params.get('from')) ? params.get('from') : null,
    to: fromIsoDate(params.get('to')) ? params.get('to') : null,
    page: Math.max(1, Math.floor(Number(params.get('page'))) || 1),
  };
}

function toQueryParams(query: AuditLogQuery): Params {
  return { ...query, page: query.page > 1 ? query.page : null };
}

// SRS §7 /admin/audit (AUD-3), Admin only.
@Component({
  selector: 'sd-audit-page',
  imports: [
    RouterLink,
    TranslatePipe,
    LocalDatePipe,
    AuditChanges,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
  ],
  templateUrl: './audit-page.html',
  styleUrls: ['../inventory/inventory-page.scss', './audit-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditPage {
  private readonly audit = inject(AuditService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly entities = auditedEntities;
  readonly columns = ['timestamp', 'user', 'entity', 'action', 'changes'];
  readonly query = signal<AuditLogQuery>(readAuditQuery(this.route.snapshot.queryParamMap));
  readonly result = signal<Paged<AuditEntry> | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly expanded = signal<ReadonlySet<string>>(new Set());

  readonly fromDate = computed(() => fromIsoDate(this.query().from));
  readonly toDate = computed(() => fromIsoDate(this.query().to));
  // The user filter comes from a click on a name; every row then has that name.
  readonly filteredUser = computed(() =>
    this.query().userId ? (this.result()?.items[0]?.userDisplayName ?? this.query().userId) : null,
  );

  private request?: Subscription;

  constructor() {
    this.route.queryParamMap.pipe(map(readAuditQuery), takeUntilDestroyed()).subscribe((query) => {
      this.query.set(query);
      this.load();
    });
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  load(): void {
    this.request?.unsubscribe();
    this.state.set('loading');
    this.expanded.set(new Set());
    this.request = this.audit.list(this.query()).subscribe({
      next: (result) => {
        this.result.set(result);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  setEntity(entity: string | null): void {
    this.update({ entity, entityId: null });
  }

  setFrom(date: Date | null): void {
    this.update({ from: date ? toIsoDate(date) : null });
  }

  setTo(date: Date | null): void {
    this.update({ to: date ? toIsoDate(date) : null });
  }

  filterByUser(userId: string | null): void {
    this.update({ userId });
  }

  // AUD-3: the full history of one entity, e.g. one booking.
  filterByEntity(entry: AuditEntry | null): void {
    this.update({ entity: entry?.entityName ?? null, entityId: entry?.entityId ?? null });
  }

  toggle(entry: AuditEntry): void {
    const next = new Set(this.expanded());
    if (!next.delete(entry.id)) {
      next.add(entry.id);
    }
    this.expanded.set(next);
  }

  goToPage(page: number): void {
    this.update({ page });
  }

  private update(changes: Partial<AuditLogQuery>): void {
    const next = { ...this.query(), ...changes, page: changes.page ?? 1 };
    void this.router.navigate([], { relativeTo: this.route, queryParams: toQueryParams(next) });
  }
}
