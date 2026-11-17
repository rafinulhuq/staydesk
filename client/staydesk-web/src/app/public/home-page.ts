import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { PropertySummary } from '../shared/inventory.models';
import { CatalogService } from './catalog.service';
import { StaySearch, StaySearchForm } from './stay-search-form';

@Component({
  selector: 'sd-home-page',
  imports: [RouterLink, TranslatePipe, StaySearchForm, MatButtonModule, MatCardModule],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  private readonly catalog = inject(CatalogService);
  private readonly router = inject(Router);

  readonly properties = signal<PropertySummary[]>([]);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');

  constructor() {
    this.load();
  }

  // Every active property is featured: the demo has a handful, and there is no
  // "featured" flag in the data model (SRS §5).
  load(): void {
    this.state.set('loading');
    this.catalog.listProperties().subscribe({
      next: (properties) => {
        this.properties.set(properties);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  search(search: StaySearch): void {
    void this.router.navigate(['/search'], { queryParams: search });
  }
}
