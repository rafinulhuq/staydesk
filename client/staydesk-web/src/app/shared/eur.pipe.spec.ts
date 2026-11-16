import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { EurPipe } from './eur.pipe';

describe('EurPipe', () => {
  function format(language: 'en' | 'de', value: number): string {
    TestBed.configureTestingModule({ providers: [provideTranslateService(), EurPipe] });
    TestBed.inject(TranslateService).use(language);
    return TestBed.inject(EurPipe).transform(value);
  }

  // Intl puts a non-breaking space (U+00A0) between amount and sign in German.
  it('formats prices the German way when the UI is in German', () => {
    expect(format('de', 1234.5)).toBe('1.234,50\u00a0€');
  });

  it('formats prices the British way when the UI is in English', () => {
    expect(format('en', 1234.5)).toBe('€1,234.50');
  });
});
