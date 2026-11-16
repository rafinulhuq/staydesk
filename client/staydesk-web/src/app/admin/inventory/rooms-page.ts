import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, map, switchMap, tap } from 'rxjs';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { EurPipe } from '../../shared/eur.pipe';
import { ActiveBookingsDialog } from './active-bookings-dialog';
import { PropertySummary, RoomDetail } from './inventory.models';
import { InventoryService } from './inventory.service';

// "2" before "10": the API orders room numbers as text (T-034).
const roomNumberOrder = new Intl.Collator(undefined, { numeric: true });

// Rooms are listed per property (the API has no flat room list); the selected
// property lives in the URL so the page can be linked and survives a reload.
@Component({
  selector: 'sd-rooms-page',
  imports: [
    RouterLink,
    TranslatePipe,
    EurPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTableModule,
  ],
  templateUrl: './rooms-page.html',
  styleUrl: './inventory-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomsPage {
  private readonly inventory = inject(InventoryService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly columns = [
    'roomNumber',
    'type',
    'maxGuests',
    'basePrice',
    'amenities',
    'status',
    'actions',
  ];
  readonly properties = signal<PropertySummary[]>([]);
  readonly propertyId = signal<string | null>(null);
  readonly rooms = signal<RoomDetail[]>([]);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly showInactive = signal(false);
  readonly visible = computed(() =>
    this.rooms()
      .filter((r) => this.showInactive() || r.isActive)
      .sort((a, b) => roomNumberOrder.compare(a.roomNumber, b.roomNumber)),
  );

  constructor() {
    this.inventory
      .listProperties()
      .pipe(
        tap((properties) => this.properties.set(properties)),
        switchMap((properties) =>
          this.route.queryParamMap.pipe(
            map((params) => params.get('propertyId') ?? properties[0]?.id ?? null),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (id) => {
          this.propertyId.set(id);
          this.loadRooms();
        },
        error: () => this.state.set('error'),
      });
  }

  selectProperty(propertyId: string): void {
    void this.router.navigate([], { queryParams: { propertyId }, replaceUrl: true });
  }

  loadRooms(): void {
    const id = this.propertyId();
    if (!id) {
      this.rooms.set([]);
      this.state.set('loaded');
      return;
    }
    this.state.set('loading');
    this.inventory.getProperty(id).subscribe({
      next: (property) => {
        this.rooms.set(property.rooms);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  amenityNames(room: RoomDetail): string {
    return room.amenities.map((a) => a.name).join(', ');
  }

  deactivate(room: RoomDetail): void {
    const data: ConfirmDialogData = {
      titleKey: 'admin.rooms.deactivate.title',
      messageKey: 'admin.rooms.deactivate.message',
      confirmKey: 'admin.common.deactivate',
      params: { roomNumber: room.roomNumber },
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() => this.inventory.deactivateRoom(room.id)),
      )
      .subscribe({
        next: ({ activeBookings }) => {
          this.loadRooms();
          if (activeBookings.length > 0) {
            this.dialog.open(ActiveBookingsDialog, { data: activeBookings });
          }
        },
        error: () => this.loadRooms(),
      });
  }

  reactivate(room: RoomDetail): void {
    this.inventory
      .reactivateRoom(room.id)
      .subscribe({ next: () => this.loadRooms(), error: () => this.loadRooms() });
  }
}
