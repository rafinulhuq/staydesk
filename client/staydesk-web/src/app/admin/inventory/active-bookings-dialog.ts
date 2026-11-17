import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TranslatePipe } from '@ngx-translate/core';
import { LocalDatePipe } from '../../shared/local-date.pipe';
import { ActiveBookingWarning } from '../../shared/inventory.models';

// PROP-5: the room is already deactivated; this tells Admin which stays still use it.
@Component({
  selector: 'sd-active-bookings-dialog',
  imports: [MatDialogModule, MatButtonModule, TranslatePipe, LocalDatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'admin.rooms.activeBookings.title' | translate }}</h2>
    <mat-dialog-content>
      <p>{{ 'admin.rooms.activeBookings.message' | translate }}</p>
      <ul>
        @for (booking of bookings; track booking.bookingId) {
          <li>
            {{ booking.reference }}: {{ booking.guestFullName }},
            {{ booking.checkIn | localDate }} – {{ booking.checkOut | localDate }}
          </li>
        }
      </ul>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button type="button" mat-dialog-close>
        {{ 'common.close' | translate }}
      </button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActiveBookingsDialog {
  readonly bookings = inject<ActiveBookingWarning[]>(MAT_DIALOG_DATA);
}
