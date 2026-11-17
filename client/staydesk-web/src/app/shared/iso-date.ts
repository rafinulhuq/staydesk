// The API's DateOnly is "yyyy-MM-dd". Conversions go through local date parts, never
// toISOString(), so a date picked shortly after midnight in Berlin doesn't become the
// previous day in UTC.
export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function fromIsoDate(value: string | null): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? '');
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
}

export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

// Midnight today in the browser's zone. The API checks "today" at the property (SRCH-2).
export function today(): Date {
  return addDays(new Date(), 0);
}

export function nightsBetween(checkIn: Date, checkOut: Date): number {
  // Rounded: a night across a DST change is 23 or 25 hours long.
  return Math.round((checkOut.getTime() - checkIn.getTime()) / 86_400_000);
}
