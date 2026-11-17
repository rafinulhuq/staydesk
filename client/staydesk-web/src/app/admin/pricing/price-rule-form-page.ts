import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EurPipe } from '../../shared/eur.pipe';
import { RoomDetail } from '../../shared/inventory.models';
import { fromIsoDate, toIsoDate, today } from '../../shared/iso-date';
import { LocalDatePipe } from '../../shared/local-date.pipe';
import {
  PricePreview,
  PriceRuleDetail,
  PriceRuleRequest,
  PriceRuleType,
  priceRuleTypes,
} from '../../shared/pricing.models';
import { applyServerErrors } from '../../shared/server-errors';
import { InventoryService } from '../inventory/inventory.service';
import { PriceRuleService } from './price-rule.service';

const roomNumberOrder = new Intl.Collator(undefined, { numeric: true });

// Create (/admin/pricing/new?propertyId=…) and edit (/admin/pricing/:id) share this page.
// The preview panel prices a room with the rule as currently typed, before saving (PRICE-8).
@Component({
  selector: 'sd-price-rule-form-page',
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
    MatSelectModule,
  ],
  templateUrl: './price-rule-form-page.html',
  styleUrls: ['../inventory/inventory-page.scss', './price-rule-form-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriceRuleFormPage {
  private readonly inventory = inject(InventoryService);
  private readonly priceRules = inject(PriceRuleService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly ruleTypes = priceRuleTypes;
  readonly today = today();
  readonly id = this.route.snapshot.paramMap.get('id');
  readonly propertyId = signal(this.route.snapshot.queryParamMap.get('propertyId'));
  readonly rooms = signal<RoomDetail[]>([]);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly submitting = signal(false);
  readonly unexpectedError = signal(false);
  readonly preview = signal<PricePreview | null>(null);
  readonly previewState = signal<'idle' | 'loading' | 'loaded' | 'invalid' | 'error'>('idle');

  // Limits mirror PriceRuleRequestValidator; which type-specific fields are required is
  // left to the API, whose field errors show under each field.
  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    // '' = every room of the property: mat-select shows a null value as nothing selected.
    roomId: [''],
    type: ['Seasonal' as PriceRuleType, Validators.required],
    percentChange: [10, [Validators.required, Validators.min(-90), Validators.max(300)]],
    startDate: [null as Date | null],
    endDate: [null as Date | null],
    minNights: [null as number | null],
    daysBeforeCheckIn: [null as number | null],
  });

  readonly previewForm = this.fb.group({
    roomId: ['', Validators.required],
    checkIn: [null as Date | null, Validators.required],
    checkOut: [null as Date | null, Validators.required],
  });

  constructor() {
    if (this.id) {
      this.priceRules.get(this.id).subscribe({
        next: (rule) => {
          this.fill(rule);
          this.loadRooms(rule.propertyId);
        },
        error: () => this.state.set('error'),
      });
    } else {
      const propertyId = this.propertyId();
      if (propertyId) {
        this.loadRooms(propertyId);
      } else {
        this.state.set('error');
      }
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.unexpectedError.set(false);
    const request = this.toRequest();
    const save = this.id
      ? this.priceRules.update(this.id, request)
      : this.priceRules.create(request);
    save.subscribe({
      next: (rule) =>
        void this.router.navigate(['/admin/pricing'], {
          queryParams: { propertyId: rule.propertyId },
        }),
      error: (error: unknown) => {
        this.submitting.set(false);
        this.unexpectedError.set(!applyServerErrors(this.form, error));
      },
    });
  }

  runPreview(): void {
    const { roomId, checkIn, checkOut } = this.previewForm.getRawValue();
    if (this.previewForm.invalid || !checkIn || !checkOut) {
      this.previewForm.markAllAsTouched();
      return;
    }
    this.previewState.set('loading');
    this.priceRules
      .preview(roomId, toIsoDate(checkIn), toIsoDate(checkOut), this.toRequest())
      .subscribe({
        next: (preview) => {
          this.preview.set(preview);
          this.previewState.set('loaded');
        },
        error: (error: unknown) => {
          this.preview.set(null);
          // Rule errors come back as "rule.percentChange"; dates as "checkIn"/"checkOut".
          const ruleErrors = applyServerErrors(this.form, error, 'rule.');
          const stayErrors = applyServerErrors(this.previewForm, error);
          this.previewState.set(ruleErrors || stayErrors ? 'invalid' : 'error');
        },
      });
  }

  // Only the chosen type's fields are sent: the API rejects values a type doesn't use (PRICE-2).
  private toRequest(): PriceRuleRequest {
    const value = this.form.getRawValue();
    const startDate =
      value.type === 'Seasonal' && value.startDate ? toIsoDate(value.startDate) : null;
    const endDate = value.type === 'Seasonal' && value.endDate ? toIsoDate(value.endDate) : null;
    return {
      // Set before the form is shown: from the query string or from the loaded rule.
      propertyId: this.propertyId()!,
      roomId: value.roomId || null,
      name: value.name.trim(),
      type: value.type,
      percentChange: value.percentChange,
      startDate,
      endDate,
      minNights: value.type === 'LengthOfStay' ? value.minNights : null,
      daysBeforeCheckIn: value.type === 'LastMinute' ? value.daysBeforeCheckIn : null,
    };
  }

  private loadRooms(propertyId: string): void {
    this.inventory.getProperty(propertyId).subscribe({
      next: (property) => {
        const rooms = [...property.rooms].sort((a, b) =>
          roomNumberOrder.compare(a.roomNumber, b.roomNumber),
        );
        this.rooms.set(rooms);
        this.previewForm.controls.roomId.setValue(
          this.form.controls.roomId.value || (rooms[0]?.id ?? ''),
        );
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  private fill(rule: PriceRuleDetail): void {
    this.propertyId.set(rule.propertyId);
    this.form.setValue({
      name: rule.name,
      roomId: rule.roomId ?? '',
      type: rule.type,
      percentChange: rule.percentChange,
      startDate: fromIsoDate(rule.startDate),
      endDate: fromIsoDate(rule.endDate),
      minNights: rule.minNights,
      daysBeforeCheckIn: rule.daysBeforeCheckIn,
    });
  }
}
