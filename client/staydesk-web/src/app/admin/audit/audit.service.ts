import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Paged } from '../../public/catalog.models';
import { AuditEntry, AuditLogQuery } from '../../shared/audit.models';

export const auditPageSize = 50;

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly http = inject(HttpClient);

  list(query: AuditLogQuery): Observable<Paged<AuditEntry>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', auditPageSize);
    for (const key of ['entity', 'entityId', 'userId', 'from', 'to'] as const) {
      const value = query[key];
      if (value) {
        params = params.set(key, value);
      }
    }
    return this.http.get<Paged<AuditEntry>>('/api/v1/audit', { params });
  }
}
