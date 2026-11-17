// ngx-translate has no plural rules. English and German both split into "one" and
// "other", so keys like "search.count.one" / "search.count.other" cover both languages.
export function plural(count: number): 'one' | 'other' {
  return count === 1 ? 'one' : 'other';
}
