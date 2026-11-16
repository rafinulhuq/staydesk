import { Routes } from '@angular/router';
import { MyBookingsPage } from './my-bookings-page';

export const accountRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'bookings' },
  { path: 'bookings', component: MyBookingsPage },
];
