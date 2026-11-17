import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { applyServerErrors, problemCode } from '../../shared/server-errors';
import { Amenity, RoomDetail, RoomType, roomTypes } from '../../shared/inventory.models';
import { InventoryService } from './inventory.service';

// Create (/admin/rooms/new?propertyId=…) and edit (/admin/rooms/:id) share this page.
@Component({
  selector: 'sd-room-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './room-form-page.html',
  styleUrl: './inventory-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomFormPage {
  private readonly inventory = inject(InventoryService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly roomTypes = roomTypes;
  readonly id = this.route.snapshot.paramMap.get('id');
  readonly propertyId = signal(this.route.snapshot.queryParamMap.get('propertyId'));
  readonly amenities = signal<Amenity[]>([]);
  readonly state = signal<'loading' | 'loaded' | 'error'>(this.id ? 'loading' : 'loaded');
  readonly submitting = signal(false);
  readonly unexpectedError = signal(false);
  // Set when the API answers 409: someone saved the room after this form loaded it.
  readonly conflict = signal(false);
  readonly loadedRoomNumber = signal('');
  private version = 0;

  // Limits mirror RoomFieldsValidator; the API stays the authority.
  readonly form = inject(NonNullableFormBuilder).group({
    roomNumber: ['', [Validators.required, Validators.maxLength(20)]],
    type: ['Double' as RoomType, Validators.required],
    maxGuests: [2, [Validators.required, Validators.min(1), Validators.max(10)]],
    basePrice: [0, [Validators.required, Validators.min(0.01)]],
    amenityIds: [[] as string[]],
    // One URL per line; the API takes a list.
    imageUrls: [''],
  });

  constructor() {
    this.inventory.listAmenities().subscribe((amenities) => this.amenities.set(amenities));
    if (this.id) {
      this.load(this.id);
    } else if (!this.propertyId()) {
      this.state.set('error');
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.unexpectedError.set(false);

    const value = this.form.getRawValue();
    const fields = {
      ...value,
      imageUrls: value.imageUrls
        .split('\n')
        .map((url) => url.trim())
        .filter(Boolean),
    };
    const save = this.id
      ? this.inventory.updateRoom(this.id, { ...fields, version: this.version })
      : // Without a propertyId the constructor already switched to the error state.
        this.inventory.createRoom({ ...fields, propertyId: this.propertyId()! });

    save.subscribe({
      next: (room) =>
        void this.router.navigate(['/admin/rooms'], {
          queryParams: { propertyId: room.propertyId },
        }),
      error: (error: unknown) => {
        this.submitting.set(false);
        if (problemCode(error) === 'concurrency_conflict') {
          this.conflict.set(true);
        } else {
          this.unexpectedError.set(!applyServerErrors(this.form, error));
        }
      },
    });
  }

  // Throws away this form's edits and shows what the other person saved. Only edit mode
  // gets a 409, so id is set.
  reload(): void {
    this.conflict.set(false);
    this.load(this.id!);
  }

  private load(id: string): void {
    this.state.set('loading');
    this.inventory.getRoom(id).subscribe({
      next: (room) => {
        this.fill(room);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  private fill(room: RoomDetail): void {
    this.version = room.version;
    this.loadedRoomNumber.set(room.roomNumber);
    this.propertyId.set(room.propertyId);
    this.form.setValue({
      roomNumber: room.roomNumber,
      type: room.type,
      maxGuests: room.maxGuests,
      basePrice: room.basePrice,
      amenityIds: room.amenities.map((a) => a.id),
      imageUrls: room.imageUrls.join('\n'),
    });
  }
}
