import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'sd-dashboard-page',
  template: '<h1>Dashboard</h1>',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {}
