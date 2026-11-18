import { formatDate } from '@angular/common';
import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '../core/i18n/language.service';

// Impure so it follows the language switch; it reads a signal, so OnPush views re-render.
// Date-only strings ("2026-03-12") are read as local dates, never shifted by time zone.
@Pipe({ name: 'localDate', pure: false })
export class LocalDatePipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  // 'short' adds the time, for timestamps such as when a guest checked in.
  transform(value: string, format: 'mediumDate' | 'short' = 'mediumDate'): string {
    return formatDate(value, format, this.language.locale());
  }
}
