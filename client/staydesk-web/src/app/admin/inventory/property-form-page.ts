import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { applyServerErrors } from '../../shared/server-errors';
import { PropertyDetail, PropertyRequest } from './inventory.models';
import { InventoryService } from './inventory.service';

// Create (/admin/properties/new) and edit (/admin/properties/:id) share this page.
@Component({
  selector: 'sd-property-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './property-form-page.html',
  styleUrl: './inventory-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PropertyFormPage {
  private readonly inventory = inject(InventoryService);
  private readonly router = inject(Router);

  readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id');
  readonly state = signal<'loading' | 'loaded' | 'error'>(this.id ? 'loading' : 'loaded');
  readonly submitting = signal(false);
  readonly unexpectedError = signal(false);

  // Limits mirror PropertyRequestValidator; the API stays the authority.
  readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    city: ['', [Validators.required, Validators.maxLength(100)]],
    address: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', Validators.maxLength(4000)],
    imageUrl: ['', Validators.maxLength(1000)],
    checkInTime: ['15:00', Validators.required],
    checkOutTime: ['11:00', Validators.required],
    timeZone: ['Europe/Berlin', Validators.required],
  });

  constructor() {
    if (this.id) {
      this.load(this.id);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.unexpectedError.set(false);

    const request = this.toRequest();
    const save = this.id
      ? this.inventory.updateProperty(this.id, request)
      : this.inventory.createProperty(request);
    save.subscribe({
      next: () => void this.router.navigateByUrl('/admin/properties'),
      error: (error: unknown) => {
        this.submitting.set(false);
        this.unexpectedError.set(!applyServerErrors(this.form, error));
      },
    });
  }

  private load(id: string): void {
    this.inventory.getProperty(id).subscribe({
      next: (property) => {
        this.form.setValue(this.toFormValue(property));
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  // The API's TimeOnly is "15:00:00"; <input type="time"> works with "15:00".
  private toFormValue(property: PropertyDetail) {
    return {
      name: property.name,
      city: property.city,
      address: property.address,
      description: property.description,
      imageUrl: property.imageUrl ?? '',
      checkInTime: property.checkInTime.slice(0, 5),
      checkOutTime: property.checkOutTime.slice(0, 5),
      timeZone: property.timeZone,
    };
  }

  private toRequest(): PropertyRequest {
    const value = this.form.getRawValue();
    return {
      ...value,
      imageUrl: value.imageUrl.trim() || null,
      checkInTime: `${value.checkInTime}:00`,
      checkOutTime: `${value.checkOutTime}:00`,
    };
  }
}
