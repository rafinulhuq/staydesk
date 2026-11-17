import { formatNumber } from '@angular/common';
import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '../../core/i18n/language.service';

// Signed and locale-formatted, so "+20 %" and "−12,5 %" read the same way in both languages.
@Pipe({ name: 'percentChange', pure: false })
export class PercentChangePipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: number): string {
    const sign = value > 0 ? '+' : value < 0 ? '−' : '';
    return `${sign}${formatNumber(Math.abs(value), this.language.locale(), '1.0-2')} %`;
  }
}
