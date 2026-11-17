import { Routes } from '@angular/router';
import { provideLocalizedDateAdapter } from '../shared/date-adapter';
import { BookPage } from './book-page';
import { ConfirmationPage } from './confirmation-page';

export const bookingRoutes: Routes = [
  {
    path: '',
    providers: [provideLocalizedDateAdapter()],
    children: [
      { path: 'confirmation/:id', component: ConfirmationPage },
      { path: ':roomId', component: BookPage },
    ],
  },
];
