import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EurPipe } from '../shared/eur.pipe';
import { PropertyDetail } from '../shared/inventory.models';
import { plural } from '../shared/plural';
import { CatalogService } from './catalog.service';

const roomNumberOrder = new Intl.Collator(undefined, { numeric: true });

@Component({
  selector: 'sd-property-page',
  imports: [RouterLink, TranslatePipe, EurPipe, MatButtonModule, MatCardModule],
  templateUrl: './property-page.html',
  styleUrl: './detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PropertyPage {
  readonly plural = plural;
  private readonly catalog = inject(CatalogService);
  // The route is properties/:id, so the parameter is always there.
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  readonly property = signal<PropertyDetail | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'notFound' | 'error'>('loading');
  readonly rooms = computed(() =>
    [...(this.property()?.rooms ?? [])].sort((a, b) =>
      roomNumberOrder.compare(a.roomNumber, b.roomNumber),
    ),
  );
  readonly images = computed(() => {
    const property = this.property();
    return property
      ? [property.imageUrl, ...property.rooms.flatMap((r) => r.imageUrls)].filter(
          (url): url is string => !!url,
        )
      : [];
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.catalog.getProperty(this.id).subscribe({
      next: (property) => {
        this.property.set(property);
        this.state.set('loaded');
      },
      error: (error: unknown) =>
        this.state.set(
          error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error',
        ),
    });
  }
}
