// Mirrors StayDesk.Application/Auditing.
export type AuditAction = 'Created' | 'Updated' | 'Deactivated' | 'Reactivated';

export const auditedEntities = ['Booking', 'Room', 'Property', 'PriceRule', 'UserRole'] as const;

export interface FieldChange {
  old: unknown;
  new: unknown;
}

export interface AuditEntry {
  id: string;
  timestamp: string;
  userId: string | null;
  userDisplayName: string;
  entityName: string;
  entityId: string;
  action: AuditAction;
  changes: Record<string, FieldChange>;
  correlationId: string | null;
}

// AUD-3. From/To are UTC days, both included.
export interface AuditLogQuery {
  entity: string | null;
  entityId: string | null;
  userId: string | null;
  from: string | null;
  to: string | null;
  page: number;
}
