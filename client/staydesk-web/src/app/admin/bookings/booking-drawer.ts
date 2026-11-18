import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { BookingService } from '../../account/booking.service';
import { BookingDetail } from '../../shared/booking.models';
import { EurPipe } from '../../shared/eur.pipe';
import { LocalDatePipe } from '../../shared/local-date.pipe';
import { BookingNights } from './booking-nights';

// Row click on the bookings table (SRS §7): read-only; actions and notes are on the detail page.
@Component({
  selector: 'sd-booking-drawer',
  imports: [
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    TranslatePipe,
    EurPipe,
    LocalDatePipe,
    BookingNights,
  ],
  template: `
    <h2 mat-dialog-title>{{ booking()?.reference ?? ('common.loading' | translate) }}</h2>
    <mat-dialog-content>
      @switch (state()) {
        @case ('loading') {
          <p>{{ 'common.loading' | translate }}</p>
        }
        @case ('error') {
          <p class="state-error" role="alert">
            {{ 'book.confirmation.loadFailed' | translate }}
            <button mat-button type="button" (click)="load()">
              {{ 'common.retry' | translate }}
            </button>
          </p>
        }
        @default {
          @if (booking(); as b) {
            <p>
              <span class="sd-booking-status" [attr.data-status]="b.status">{{
                'bookingStatus.' + b.status | translate
              }}</span>
            </p>
            <dl>
              <dt>{{ 'admin.bookings.fields.guest' | translate }}</dt>
              <dd>
                {{ b.guestFullName }}<br />{{ b.guestEmail }}
                @if (b.guestPhone) {
                  <br />{{ b.guestPhone }}
                }
              </dd>
              <dt>{{ 'admin.bookings.fields.room' | translate }}</dt>
              <dd>
                {{ b.propertyName }}, {{ 'search.card.room' | translate: { number: b.roomNumber } }}
              </dd>
              <dt>{{ 'book.dates' | translate }}</dt>
              <dd>{{ b.checkIn | localDate }} – {{ b.checkOut | localDate }}</dd>
              <dt>{{ 'search.fields.guests' | translate }}</dt>
              <dd>{{ b.guests }}</dd>
              @if (b.specialRequests) {
                <dt>{{ 'book.fields.specialRequests' | translate }}</dt>
                <dd>{{ b.specialRequests }}</dd>
              }
              @if (b.cancellationFee !== null) {
                <dt>{{ 'admin.bookings.fields.cancellationFee' | translate }}</dt>
                <dd>
                  {{ b.cancellationFee | eur }}
                  @if (b.feeWaived) {
                    ({{ 'admin.bookings.fields.feeWaived' | translate }})
                  }
                </dd>
              }
            </dl>
            <sd-booking-nights [booking]="b" />
          }
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">{{ 'common.close' | translate }}</button>
      <a mat-flat-button mat-dialog-close [routerLink]="['/admin/bookings', id]">
        {{ 'admin.bookings.openDetail' | translate }}
      </a>
    </mat-dialog-actions>
  `,
  styles: `
    /* A side sheet: the details fill the height, so Close sits at the bottom. */
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    mat-dialog-content {
      flex: 1;
      max-height: none;
    }
    dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: var(--sd-space) calc(var(--sd-space) * 2);
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
    }
    .state-error {
      color: var(--mat-sys-error);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingDrawer {
  private readonly bookings = inject(BookingService);
  readonly id = inject<string>(MAT_DIALOG_DATA);

  readonly booking = signal<BookingDetail | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.bookings.get(this.id).subscribe({
      next: (booking) => {
        this.booking.set(booking);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }
}
