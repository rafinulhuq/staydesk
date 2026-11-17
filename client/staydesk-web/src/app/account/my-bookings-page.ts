import { formatCurrency } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { filter, switchMap } from 'rxjs';
import { LanguageService } from '../core/i18n/language.service';
import { BookingSummary, MyBookingsScope } from '../shared/booking.models';
import { ConfirmDialog, ConfirmDialogData } from '../shared/confirm-dialog';
import { EurPipe } from '../shared/eur.pipe';
import { LocalDatePipe } from '../shared/local-date.pipe';
import { plural } from '../shared/plural';
import { problemCode } from '../shared/server-errors';
import { BookingService } from './booking.service';

const scopes: readonly MyBookingsScope[] = ['Upcoming', 'Past'];

// SRS §7 /account/bookings: upcoming and past tabs; cancelling shows the fee first (BOOK-5).
@Component({
  selector: 'sd-my-bookings-page',
  imports: [RouterLink, TranslatePipe, EurPipe, LocalDatePipe, MatButtonModule, MatTabsModule],
  templateUrl: './my-bookings-page.html',
  styleUrls: ['../public/detail-page.scss', './my-bookings-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyBookingsPage {
  private readonly bookings = inject(BookingService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  readonly plural = plural;
  readonly scopes = scopes;
  readonly scope = signal<MyBookingsScope>('Upcoming');
  readonly items = signal<BookingSummary[]>([]);
  readonly state = signal<'loading' | 'loaded' | 'error'>('loading');

  constructor() {
    this.load();
  }

  selectTab(index: number): void {
    this.scope.set(scopes[index]);
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.bookings.mine(this.scope()).subscribe({
      next: (items) => {
        this.items.set(items);
        this.state.set('loaded');
      },
      error: () => this.state.set('error'),
    });
  }

  pay(booking: BookingSummary): void {
    this.bookings.pay(booking.id).subscribe({
      next: () => this.load(),
      error: (error: unknown) =>
        this.notify(
          problemCode(error) === 'booking_expired'
            ? 'account.bookings.expired'
            : 'account.bookings.actionFailed',
        ),
    });
  }

  // The quote comes first, so the guest sees the fee before confirming.
  cancel(booking: BookingSummary): void {
    this.bookings
      .cancellationQuote(booking.id)
      .pipe(
        switchMap((quote) =>
          this.dialog
            .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
              data: {
                titleKey: 'account.bookings.cancelTitle',
                messageKey:
                  quote.amountPaid === 0
                    ? 'account.bookings.cancelUnpaid'
                    : quote.fee === 0
                      ? 'account.bookings.cancelFree'
                      : quote.refund === 0
                        ? 'account.bookings.cancelNoRefund'
                        : 'account.bookings.cancelWithFee',
                confirmKey: 'account.bookings.cancelConfirm',
                dismissKey: 'account.bookings.keep',
                params: {
                  reference: booking.reference,
                  fee: this.money(quote.fee),
                  refund: this.money(quote.refund),
                },
              },
            })
            .afterClosed(),
        ),
        filter((confirmed) => confirmed === true),
        switchMap(() => this.bookings.cancel(booking.id)),
      )
      .subscribe({
        next: () => {
          this.notify('account.bookings.cancelled');
          this.load();
        },
        error: () => this.notify('account.bookings.actionFailed'),
      });
  }

  private money(value: number): string {
    return formatCurrency(value, this.language.locale(), '€', 'EUR', '1.2-2');
  }

  private notify(key: string): void {
    this.snackBar.open(this.translate.instant(key), this.translate.instant('common.close'), {
      duration: 6000,
    });
  }
}
