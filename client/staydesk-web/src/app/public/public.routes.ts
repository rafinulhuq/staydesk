import { Routes } from '@angular/router';
import { provideLocalizedDateAdapter } from '../shared/date-adapter';
import { HomePage } from './home-page';
import { LoginPage } from './login-page';
import { PropertyPage } from './property-page';
import { RegisterPage } from './register-page';
import { RoomPage } from './room-page';
import { SearchPage } from './search-page';

export const publicRoutes: Routes = [
  {
    path: '',
    providers: [provideLocalizedDateAdapter()],
    children: [
      { path: '', component: HomePage },
      { path: 'search', component: SearchPage },
      { path: 'properties/:id', component: PropertyPage },
      { path: 'rooms/:id', component: RoomPage },
      { path: 'login', component: LoginPage },
      { path: 'register', component: RegisterPage },
    ],
  },
];
