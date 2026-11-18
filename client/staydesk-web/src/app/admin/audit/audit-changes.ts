import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { FieldChange } from '../../shared/audit.models';

// AUD-3 "JSON diff view": one row per changed field, old next to new. Field names are the
// entity's property names as stored, so they are shown as they are, not translated.
@Component({
  selector: 'sd-audit-changes',
  imports: [TranslatePipe],
  template: `
    <table>
      <thead>
        <tr>
          <th scope="col">{{ 'admin.audit.field' | translate }}</th>
          <th scope="col">{{ 'admin.audit.old' | translate }}</th>
          <th scope="col">{{ 'admin.audit.new' | translate }}</th>
        </tr>
      </thead>
      <tbody>
        @for (row of rows(); track row.field) {
          <tr>
            <th scope="row">{{ row.field }}</th>
            <td class="old">{{ row.old }}</td>
            <td>{{ row.new }}</td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    table {
      border-collapse: collapse;
      font: var(--mat-sys-body-small);
    }
    th,
    td {
      padding: calc(var(--sd-space) / 2) var(--sd-space);
      text-align: start;
      vertical-align: top;
      overflow-wrap: anywhere;
    }
    thead th {
      color: var(--mat-sys-on-surface-variant);
      font-weight: 500;
    }
    .old {
      color: var(--mat-sys-on-surface-variant);
      text-decoration: line-through;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditChanges {
  readonly changes = input.required<Record<string, FieldChange>>();

  readonly rows = computed(() =>
    Object.entries(this.changes()).map(([field, change]) => ({
      field,
      old: show(change.old),
      new: show(change.new),
    })),
  );
}

function show(value: unknown): string {
  if (value === null || value === undefined) {
    return '–';
  }
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}
