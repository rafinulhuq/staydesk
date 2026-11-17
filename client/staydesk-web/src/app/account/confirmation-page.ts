import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { BookingDetail } from '../shared/booking.models';
import { EurPipe } from '../shared/eur.pipe';
import { LocalDatePipe } from '../shared/local-date.pipe';
import { BookingService } from './booking.service';

@Component({
  selector: 'sd-confirmation-page',
  imports: [RouterLink, TranslatePipe, EurPipe, LocalDatePipe, MatButtonModule],
  template: `
    @switch (state()) {
      @case ('loading') {
        <p class="state">{{ 'common.loading' | translate }}</p>
      }
      @case ('notFound') {
        <p class="state">{{ 'book.confirmation.notFound' | translate }}</p>
      }
      @case ('error') {
        <p class="state state-error" role="alert">
          {{ 'book.confirmation.loadFailed' | translate }}
          <button mat-button type="button" (click)="load()">
            {{ 'common.retry' | translate }}
          </button>
        </p>
      }
      @default {
        @if (booking(); as b) {
          <header class="page-title">
            <h1>{{ 'book.confirmation.title' | translate }}</h1>
          </header>
          <p class="reference">{{ b.reference }}</p>
          <dl class="facts">
            <dt>{{ 'book.confirmation.place' | translate }}</dt>
            <dd>
              {{ b.propertyName }}, {{ 'search.card.room' | translate: { number: b.roomNumber } }}
            </dd>
            <dt>{{ 'book.dates' | translate }}</dt>
            <dd>{{ b.checkIn | localDate }} – {{ b.checkOut | localDate }}</dd>
            <dt>{{ 'search.fields.guests' | translate }}</dt>
            <dd>{{ b.guests }}</dd>
            <dt>{{ 'book.total' | translate }}</dt>
            <dd>{{ b.totalPrice | eur }}</dd>
          </dl>
          <p class="hint">{{ 'book.confirmation.email' | translate: { email: b.guestEmail } }}</p>
          <a mat-flat-button routerLink="/account/bookings">{{ 'nav.myBookings' | translate }}</a>
        }
      }
    }
  `,
  styleUrls: ['../public/detail-page.scss', './book-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmationPage {
  private readonly bookings = inject(BookingService);
  // The route is book/confirmation/:id, so the parameter is always there.
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  readonly booking = signal<BookingDetail | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'notFound' | 'error'>('loading');

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.bookings.get(this.id).subscribe({
      next: (booking) => {
        this.booking.set(booking);
        this.state.set('loaded');
      },
      error: (error: unknown) =>
        this.state.set(
          error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error',
        ),
    });
  }
}
