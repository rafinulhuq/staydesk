import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, switchMap } from 'rxjs';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { PropertySummary } from './inventory.models';
import { InventoryService } from './inventory.service';

@Component({
  selector: 'sd-properties-page',
  imports: [RouterLink, TranslatePipe, MatButtonModule, MatSlideToggleModule, MatTableModule],
  templateUrl: './properties-page.html',
  styleUrl: './inventory-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PropertiesPage {
  private readonly inventory = inject(InventoryService);
  private readonly dialog = inject(MatDialog);

  readonly columns = ['name', 'city', 'address', 'status', 'actions'];
  readonly properties = signal<PropertySummary[]>([]);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly showInactive = signal(false);
  readonly visible = computed(() =>
    this.showInactive() ? this.properties() : this.properties().filter((p) => p.isActive),
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.inventory.listProperties().subscribe({
      next: (properties) => {
        this.properties.set(properties);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  deactivate(property: PropertySummary): void {
    const data: ConfirmDialogData = {
      titleKey: 'admin.properties.deactivate.title',
      messageKey: 'admin.properties.deactivate.message',
      confirmKey: 'admin.common.deactivate',
      params: { name: property.name },
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() => this.inventory.deactivateProperty(property.id)),
      )
      .subscribe({ next: () => this.load(), error: () => this.load() });
  }

  reactivate(property: PropertySummary): void {
    this.inventory
      .reactivateProperty(property.id)
      .subscribe({ next: () => this.load(), error: () => this.load() });
  }
}
