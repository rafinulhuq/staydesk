import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'sd-my-bookings-page',
  template: '<h1>My bookings</h1>',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyBookingsPage {}
