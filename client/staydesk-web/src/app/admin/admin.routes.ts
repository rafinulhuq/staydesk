import { Routes } from '@angular/router';
import { roleGuard } from '../core/auth/role.guard';
import { AdminLayout } from './admin-layout';
import { DashboardPage } from './dashboard-page';
import { PropertiesPage } from './inventory/properties-page';
import { PropertyFormPage } from './inventory/property-form-page';
import { RoomFormPage } from './inventory/room-form-page';
import { RoomsPage } from './inventory/rooms-page';

export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      { path: '', component: DashboardPage },
      // Inventory is Admin-only (SRS §7); the area itself lets Staff in.
      {
        path: '',
        canMatch: [roleGuard('Admin')],
        children: [
          { path: 'properties', component: PropertiesPage },
          { path: 'properties/new', component: PropertyFormPage },
          { path: 'properties/:id', component: PropertyFormPage },
          { path: 'rooms', component: RoomsPage },
          { path: 'rooms/new', component: RoomFormPage },
          { path: 'rooms/:id', component: RoomFormPage },
        ],
      },
    ],
  },
];
