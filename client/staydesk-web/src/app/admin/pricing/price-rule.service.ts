import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PriceRuleDetail, PriceRuleRequest, PricePreview } from '../../shared/pricing.models';

@Injectable({ providedIn: 'root' })
export class PriceRuleService {
  private readonly http = inject(HttpClient);

  list(propertyId: string): Observable<PriceRuleDetail[]> {
    return this.http.get<PriceRuleDetail[]>('/api/v1/price-rules', { params: { propertyId } });
  }

  get(id: string): Observable<PriceRuleDetail> {
    return this.http.get<PriceRuleDetail>(`/api/v1/price-rules/${id}`);
  }

  create(request: PriceRuleRequest): Observable<PriceRuleDetail> {
    return this.http.post<PriceRuleDetail>('/api/v1/price-rules', request);
  }

  update(id: string, request: PriceRuleRequest): Observable<PriceRuleDetail> {
    return this.http.put<PriceRuleDetail>(`/api/v1/price-rules/${id}`, request);
  }

  deactivate(id: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/price-rules/${id}`);
  }

  // PRICE-8: nothing is saved.
  preview(
    roomId: string,
    checkIn: string,
    checkOut: string,
    rule: PriceRuleRequest,
  ): Observable<PricePreview> {
    return this.http.post<PricePreview>('/api/v1/pricing/preview', {
      roomId,
      checkIn,
      checkOut,
      rule,
    });
  }
}
