import {
  EnvironmentProviders,
  Provider,
  effect,
  inject,
  provideEnvironmentInitializer,
} from '@angular/core';
import { DateAdapter, provideNativeDateAdapter } from '@angular/material/core';
import { LanguageService } from '../core/i18n/language.service';

// Date pickers follow the header's language switch (month names, first day of the week).
export function provideLocalizedDateAdapter(): (Provider | EnvironmentProviders)[] {
  return [
    provideNativeDateAdapter(),
    provideEnvironmentInitializer(() => {
      const adapter = inject(DateAdapter);
      const language = inject(LanguageService);
      effect(() => adapter.setLocale(language.locale()));
    }),
  ];
}
