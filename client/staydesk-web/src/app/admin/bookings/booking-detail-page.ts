import { formatCurrency } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Observable, filter, switchMap } from 'rxjs';
import { BookingService } from '../../account/booking.service';
import { LanguageService } from '../../core/i18n/language.service';
import { BookingDetail } from '../../shared/booking.models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { EurPipe } from '../../shared/eur.pipe';
import { LocalDatePipe } from '../../shared/local-date.pipe';
import { problemCode } from '../../shared/server-errors';
import { BookingNights } from './booking-nights';

export const maxNotesLength = 2000;

type Notice = 'notAllowed' | 'changedElsewhere' | 'failed' | null;

// SRS §7 /admin/bookings/:id (BO-2). The server decides whether an action is allowed today;
// the page only offers the actions that fit the current status. The history timeline is 5c.
@Component({
  selector: 'sd-booking-detail-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    EurPipe,
    LocalDatePipe,
    BookingNights,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './booking-detail-page.html',
  styleUrls: ['../inventory/inventory-page.scss', './booking-detail-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingDetailPage {
  readonly maxNotesLength = maxNotesLength;
  private readonly bookings = inject(BookingService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);
  // The route is bookings/:id, so the parameter is always there.
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  readonly booking = signal<BookingDetail | null>(null);
  readonly state = signal<'loading' | 'loaded' | 'notFound' | 'error'>('loading');
  readonly notice = signal<Notice>(null);
  readonly busy = signal(false);
  readonly waiveFee = signal(false);
  readonly notes = inject(NonNullableFormBuilder).control('', Validators.maxLength(maxNotesLength));

  readonly canCheckIn = computed(() => this.booking()?.status === 'Confirmed');
  readonly canCheckOut = computed(() => this.booking()?.status === 'CheckedIn');
  readonly canMarkNoShow = computed(() => this.booking()?.status === 'Confirmed');
  readonly canCancel = computed(() =>
    ['Pending', 'Confirmed'].includes(this.booking()?.status ?? ''),
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.bookings.get(this.id).subscribe({
      next: (booking) => this.show(booking),
      error: (error: unknown) =>
        this.state.set(
          error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error',
        ),
    });
  }

  checkIn(): void {
    this.run(this.bookings.checkIn(this.id));
  }

  checkOut(): void {
    this.run(this.bookings.checkOut(this.id));
  }

  // BOOK-5: the first night is charged, so staff confirm first.
  markNoShow(booking: BookingDetail): void {
    this.confirm({
      titleKey: 'admin.booking.noShowTitle',
      messageKey: 'admin.booking.noShowMessage',
      confirmKey: 'admin.booking.markNoShow',
      params: { reference: booking.reference, fee: this.money(booking.priceBreakdown[0].price) },
    }).subscribe(() => this.run(this.bookings.markNoShow(this.id)));
  }

  // The quote shows what the guest gets back before staff confirm; a waived fee refunds everything.
  cancel(booking: BookingDetail): void {
    const waive = this.waiveFee();
    this.bookings
      .cancellationQuote(this.id)
      .pipe(
        switchMap((quote) =>
          this.confirm({
            titleKey: 'account.bookings.cancelTitle',
            messageKey:
              quote.amountPaid === 0
                ? 'account.bookings.cancelUnpaid'
                : waive
                  ? 'admin.booking.cancelWaived'
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
              refund: this.money(waive ? quote.amountPaid : quote.refund),
            },
          }),
        ),
      )
      .subscribe({
        next: () => this.run(this.bookings.cancel(this.id, waive)),
        error: (error: unknown) => this.fail(error),
      });
  }

  saveNotes(): void {
    if (this.notes.invalid) {
      return;
    }
    this.busy.set(true);
    this.bookings.updateNotes(this.id, this.notes.value).subscribe({
      next: (booking) => {
        this.show(booking);
        this.snackBar.open(
          this.translate.instant('admin.booking.notesSaved'),
          this.translate.instant('common.close'),
          { duration: 4000 },
        );
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  private run(action: Observable<BookingDetail>): void {
    this.busy.set(true);
    this.notice.set(null);
    action.subscribe({
      next: (booking) => this.show(booking),
      error: (error: unknown) => this.fail(error),
    });
  }

  private show(booking: BookingDetail): void {
    this.booking.set(booking);
    this.notes.reset(booking.internalNotes ?? '');
    this.waiveFee.set(false);
    this.busy.set(false);
    this.state.set('loaded');
  }

  private fail(error: unknown): void {
    this.busy.set(false);
    switch (problemCode(error)) {
      case 'invalid_status_transition':
        this.notice.set('notAllowed');
        return;
      // Someone else changed the booking first (xmin); show their version.
      case 'concurrency_conflict':
        this.notice.set('changedElsewhere');
        this.load();
        return;
    }
    this.notice.set('failed');
  }

  private confirm(data: ConfirmDialogData): Observable<true> {
    return this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data })
      .afterClosed()
      .pipe(filter((confirmed): confirmed is true => confirmed === true));
  }

  private money(amount: number): string {
    return formatCurrency(amount, this.language.locale(), '€', 'EUR', '1.2-2');
  }
}
