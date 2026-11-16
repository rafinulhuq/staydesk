import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'sd-my-bookings-page',
  imports: [TranslatePipe],
  template: "<h1>{{ 'account.bookings.title' | translate }}</h1>",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyBookingsPage {}
