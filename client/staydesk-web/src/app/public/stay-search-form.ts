import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@ngx-translate/core';
import { fromIsoDate, nightsBetween, toIsoDate, today } from '../shared/iso-date';

export interface StaySearch {
  city: string;
  checkIn: string;
  checkOut: string;
  guests: number;
}

const maxNights = 30;

// SRCH-2, mirrored for quick feedback; the API stays the authority.
function maxStay(group: AbstractControl): ValidationErrors | null {
  const { checkIn, checkOut } = group.value as { checkIn: Date | null; checkOut: Date | null };
  return checkIn && checkOut && nightsBetween(checkIn, checkOut) > maxNights
    ? { maxStay: true }
    : null;
}

// City, dates and guests: the hero bar on the home page and the bar above search results.
@Component({
  selector: 'sd-stay-search-form',
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './stay-search-form.html',
  styleUrl: './stay-search-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaySearchForm {
  readonly value = input<StaySearch | null>(null);
  readonly searched = output<StaySearch>();

  readonly today = today();
  readonly maxNights = maxNights;
  readonly form = inject(NonNullableFormBuilder).group(
    {
      city: ['', [Validators.required, Validators.maxLength(100)]],
      checkIn: [null as Date | null, Validators.required],
      checkOut: [null as Date | null, Validators.required],
      guests: [2, [Validators.required, Validators.min(1), Validators.max(10)]],
    },
    { validators: maxStay },
  );

  constructor() {
    effect(() => {
      const value = this.value();
      if (value) {
        this.form.setValue({
          city: value.city,
          checkIn: fromIsoDate(value.checkIn),
          checkOut: fromIsoDate(value.checkOut),
          guests: value.guests,
        });
      }
    });
  }

  submit(): void {
    const { city, checkIn, checkOut, guests } = this.form.getRawValue();
    if (this.form.invalid || !checkIn || !checkOut) {
      this.form.markAllAsTouched();
      return;
    }
    this.searched.emit({
      city: city.trim(),
      checkIn: toIsoDate(checkIn),
      checkOut: toIsoDate(checkOut),
      guests,
    });
  }
}
