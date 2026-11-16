import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'sd-dashboard-page',
  imports: [TranslatePipe],
  template: "<h1>{{ 'admin.dashboard.title' | translate }}</h1>",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {}
