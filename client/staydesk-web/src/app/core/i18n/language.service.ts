import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export type Language = 'en' | 'de';

const languageKey = 'staydesk.language';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);

  readonly current = this.translate.currentLang;

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
