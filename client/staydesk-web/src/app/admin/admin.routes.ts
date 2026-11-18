import { Routes } from '@angular/router';
import { roleGuard } from '../core/auth/role.guard';
import { AdminLayout } from './admin-layout';
import { AuditPage } from './audit/audit-page';
import { BookingDetailPage } from './bookings/booking-detail-page';
import { BookingsPage } from './bookings/bookings-page';
import { DashboardPage } from './dashboard-page';
import { PropertiesPage } from './inventory/properties-page';
import { PropertyFormPage } from './inventory/property-form-page';
import { RoomFormPage } from './inventory/room-form-page';
import { RoomsPage } from './inventory/rooms-page';
import { PriceRuleFormPage } from './pricing/price-rule-form-page';
import { PriceRulesPage } from './pricing/price-rules-page';
import { provideLocalizedDateAdapter } from '../shared/date-adapter';

export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      { path: '', component: DashboardPage },
      { path: 'bookings', component: BookingsPage, providers: [provideLocalizedDateAdapter()] },
      { path: 'bookings/:id', component: BookingDetailPage },
      // Inventory and pricing are Admin-only (SRS §7); the area itself lets Staff in.
      {
        path: '',
        canMatch: [roleGuard('Admin')],
        providers: [provideLocalizedDateAdapter()],
        children: [
          { path: 'properties', component: PropertiesPage },
          { path: 'properties/new', component: PropertyFormPage },
          { path: 'properties/:id', component: PropertyFormPage },
          { path: 'rooms', component: RoomsPage },
          { path: 'rooms/new', component: RoomFormPage },
          { path: 'rooms/:id', component: RoomFormPage },
          { path: 'pricing', component: PriceRulesPage },
          { path: 'pricing/new', component: PriceRuleFormPage },
          { path: 'pricing/:id', component: PriceRuleFormPage },
          { path: 'audit', component: AuditPage },
        ],
      },
    ],
  },
];
