import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TranslatePipe } from '@ngx-translate/core';

export interface ConfirmDialogData {
  titleKey: string;
  messageKey: string;
  confirmKey: string;
  // Defaults to "Cancel"; a cancel-booking dialog needs "Keep booking" instead.
  dismissKey?: string;
  params?: Record<string, string>;
}

// Closes with true on confirm; Esc, backdrop and Cancel close with undefined.
@Component({
  selector: 'sd-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ data.titleKey | translate: data.params }}</h2>
    <mat-dialog-content>{{ data.messageKey | translate: data.params }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>
        {{ data.dismissKey ?? 'common.cancel' | translate }}
      </button>
      <button mat-flat-button type="button" [mat-dialog-close]="true">
        {{ data.confirmKey | translate }}
      </button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialog {
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
