import { formatCurrency } from '@angular/common';
import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '../core/i18n/language.service';

// SRS §7: prices are always EUR, formatted for the chosen language (de-DE: "89,00 €").
@Pipe({ name: 'eur', pure: false })
export class EurPipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: number): string {
    return formatCurrency(value, this.language.locale(), '€', 'EUR', '1.2-2');
  }
}
