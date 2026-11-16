import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'sd-home-page',
  imports: [TranslatePipe],
  template: "<h1>{{ 'home.title' | translate }}</h1>",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {}
