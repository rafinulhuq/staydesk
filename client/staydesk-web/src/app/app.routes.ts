import { Routes } from '@angular/router';

// Each area is a separate lazy chunk, so guests never download admin code.
export const routes: Routes = [
  { path: '', loadChildren: () => import('./public/public.routes').then((m) => m.publicRoutes) },
  { path: 'account', loadChildren: () => import('./account/account.routes').then((m) => m.accountRoutes) },
  { path: 'admin', loadChildren: () => import('./admin/admin.routes').then((m) => m.adminRoutes) },
  { path: '**', redirectTo: '' },
];
