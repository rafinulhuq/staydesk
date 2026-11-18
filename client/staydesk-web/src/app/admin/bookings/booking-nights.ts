import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { BookingDetail } from '../../shared/booking.models';
import { EurPipe } from '../../shared/eur.pipe';
import { LocalDatePipe } from '../../shared/local-date.pipe';

// The stored price snapshot, one row per night (PRICE-*), in the drawer and the detail page.
@Component({
  selector: 'sd-booking-nights',
  imports: [TranslatePipe, EurPipe, LocalDatePipe],
  template: `
    <table>
      <caption>
        {{
          'admin.bookings.fields.priceBreakdown' | translate
        }}
      </caption>
      @for (night of booking().priceBreakdown; track night.date) {
        <tr>
          <td>{{ night.date | localDate }}</td>
          <td class="amount">{{ night.price | eur }}</td>
        </tr>
      }
      <tr class="total">
        <td>{{ 'book.total' | translate }}</td>
        <td class="amount">{{ booking().totalPrice | eur }}</td>
      </tr>
    </table>
  `,
  styles: `
    table {
      width: 100%;
      max-width: 400px;
      border-collapse: collapse;
    }
    caption {
      text-align: start;
      font: var(--mat-sys-title-small);
      padding-bottom: var(--sd-space);
    }
    td {
      padding-block: calc(var(--sd-space) / 2);
    }
    .amount {
      text-align: end;
    }
    .total td {
      border-top: 1px solid var(--mat-sys-outline-variant);
      font-weight: 500;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingNights {
  readonly booking = input.required<BookingDetail>();
}
