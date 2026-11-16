import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'sd-home-page',
  template: '<h1>StayDesk</h1>',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {}
