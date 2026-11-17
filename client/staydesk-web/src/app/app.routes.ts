import { Routes } from '@angular/router';
import { roleGuard } from './core/auth/role.guard';

// Each area is a separate lazy chunk, so guests never download admin code.
export const routes: Routes = [
  {
    path: 'account',
    canMatch: [roleGuard('Guest')],
    loadChildren: () => import('./account/account.routes').then((m) => m.accountRoutes),
  },
  // SRS §7: /book/:roomId is for guests; it shares the account area's chunk.
  {
    path: 'book',
    canMatch: [roleGuard('Guest')],
    loadChildren: () => import('./account/booking.routes').then((m) => m.bookingRoutes),
  },
  {
    path: 'admin',
    canMatch: [roleGuard('Staff')],
    loadChildren: () => import('./admin/admin.routes').then((m) => m.adminRoutes),
  },
  { path: '', loadChildren: () => import('./public/public.routes').then((m) => m.publicRoutes) },
  { path: '**', redirectTo: '' },
];
