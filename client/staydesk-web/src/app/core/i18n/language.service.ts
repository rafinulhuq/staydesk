import { registerLocaleData } from '@angular/common';
import localeDe from '@angular/common/locales/de';
import localeEnGb from '@angular/common/locales/en-GB';
import { Injectable, computed, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export type Language = 'en' | 'de';

const languageKey = 'staydesk.language';

// Registered here rather than in main.ts, so tests that use the locale pipes get them too.
registerLocaleData(localeDe, 'de-DE');
registerLocaleData(localeEnGb, 'en-GB');

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);

  readonly current = this.translate.currentLang;
  readonly locale = computed(() => (this.current() === 'de' ? 'de-DE' : 'en-GB'));

  // Saved choice first, then the browser's preference, then English.
  initialLanguage(): Language {
    const saved = localStorage.getItem(languageKey);
    if (saved === 'en' || saved === 'de') {
      return saved;
    }
    return navigator.language.toLowerCase().startsWith('de') ? 'de' : 'en';
  }

  use(language: Language): void {
    localStorage.setItem(languageKey, language);
    this.translate.use(language);
    document.documentElement.lang = language;
  }
}
