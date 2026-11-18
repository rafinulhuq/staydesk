import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'sd-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatTabsModule, TranslatePipe],
  template: `
    <nav
      mat-tab-nav-bar
      mat-stretch-tabs="false"
      [tabPanel]="panel"
      [attr.aria-label]="'nav.admin' | translate"
    >
      @for (link of links(); track link.path) {
        <a
          mat-tab-link
          [routerLink]="link.path"
          routerLinkActive
          #active="routerLinkActive"
          [routerLinkActiveOptions]="{ exact: link.path === '/admin' }"
          [active]="active.isActive"
          >{{ link.key | translate }}</a
        >
      }
    </nav>
    <mat-tab-nav-panel #panel>
      <router-outlet />
    </mat-tab-nav-panel>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLayout {
  private readonly auth = inject(AuthService);

  protected readonly links = computed(() => {
    const links = [
      { path: '/admin', key: 'admin.nav.dashboard' },
      { path: '/admin/bookings', key: 'admin.nav.bookings' },
    ];
    if (this.auth.hasRole('Admin')) {
      links.push(
        { path: '/admin/properties', key: 'admin.nav.properties' },
        { path: '/admin/rooms', key: 'admin.nav.rooms' },
        { path: '/admin/pricing', key: 'admin.nav.pricing' },
        { path: '/admin/audit', key: 'admin.nav.audit' },
      );
    }
    return links;
  });
}
