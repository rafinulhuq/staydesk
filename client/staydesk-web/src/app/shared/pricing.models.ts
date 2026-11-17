// Mirrors the API contracts in StayDesk.Application/Pricing. The room page shows quotes;
// the price rules admin page manages rules and previews them.
export type PriceRuleType = 'Seasonal' | 'Weekend' | 'LengthOfStay' | 'LastMinute';

export const priceRuleTypes: readonly PriceRuleType[] = [
  'Seasonal',
  'Weekend',
  'LengthOfStay',
  'LastMinute',
];

export interface PriceRuleRequest {
  propertyId: string;
  roomId: string | null;
  name: string;
  type: PriceRuleType;
  percentChange: number;
  startDate: string | null;
  endDate: string | null;
  minNights: number | null;
  daysBeforeCheckIn: number | null;
}

export interface PriceRuleDetail extends PriceRuleRequest {
  id: string;
}

export interface AppliedRuleSummary {
  name: string;
  type: string;
  percentChange: number;
}

export interface QuoteNight {
  date: string;
  basePrice: number;
  rules: AppliedRuleSummary[];
  price: number;
}

export interface StayQuote {
  roomId: string;
  checkIn: string;
  checkOut: string;
  nights: QuoteNight[];
  total: number;
}

export interface PricePreview {
  current: StayQuote;
  withRule: StayQuote;
}
