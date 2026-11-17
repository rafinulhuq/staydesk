import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, forkJoin, map, switchMap, tap } from 'rxjs';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { PropertySummary } from '../../shared/inventory.models';
import { LocalDatePipe } from '../../shared/local-date.pipe';
import { PriceRuleDetail } from '../../shared/pricing.models';
import { InventoryService } from '../inventory/inventory.service';
import { PriceRuleService } from './price-rule.service';
import { PercentChangePipe } from './percent-change.pipe';

// Rules are listed per property, like rooms; the selected property lives in the URL.
@Component({
  selector: 'sd-price-rules-page',
  imports: [
    RouterLink,
    TranslatePipe,
    LocalDatePipe,
    PercentChangePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTableModule,
  ],
  templateUrl: './price-rules-page.html',
  styleUrl: '../inventory/inventory-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriceRulesPage {
  private readonly inventory = inject(InventoryService);
  private readonly priceRules = inject(PriceRuleService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly columns = ['name', 'appliesTo', 'type', 'percentChange', 'details', 'actions'];
  readonly properties = signal<PropertySummary[]>([]);
  readonly propertyId = signal<string | null>(null);
  readonly rules = signal<PriceRuleDetail[]>([]);
  readonly roomNumbers = signal(new Map<string, string>());
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly hasProperties = computed(() => this.properties().length > 0);

  constructor() {
    this.inventory
      .listProperties()
      .pipe(
        tap((properties) => this.properties.set(properties)),
        switchMap((properties) =>
          this.route.queryParamMap.pipe(
            map((params) => params.get('propertyId') ?? properties[0]?.id ?? null),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (id) => {
          this.propertyId.set(id);
          this.loadRules();
        },
        error: () => this.state.set('error'),
      });
  }

  selectProperty(propertyId: string): void {
    void this.router.navigate([], { queryParams: { propertyId }, replaceUrl: true });
  }

  loadRules(): void {
    const id = this.propertyId();
    if (!id) {
      this.rules.set([]);
      this.state.set('loaded');
      return;
    }
    this.state.set('loading');
    forkJoin({
      rules: this.priceRules.list(id),
      property: this.inventory.getProperty(id),
    }).subscribe({
      next: ({ rules, property }) => {
        this.rules.set(rules);
        this.roomNumbers.set(new Map(property.rooms.map((r) => [r.id, r.roomNumber])));
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  deactivate(rule: PriceRuleDetail): void {
    const data: ConfirmDialogData = {
      titleKey: 'admin.pricing.deactivate.title',
      messageKey: 'admin.pricing.deactivate.message',
      confirmKey: 'admin.common.deactivate',
      params: { name: rule.name },
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() => this.priceRules.deactivate(rule.id)),
      )
      .subscribe({ next: () => this.loadRules(), error: () => this.loadRules() });
  }
}
