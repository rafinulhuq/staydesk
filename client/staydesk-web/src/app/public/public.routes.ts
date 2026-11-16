import { Routes } from '@angular/router';
import { HomePage } from './home-page';
import { LoginPage } from './login-page';
import { RegisterPage } from './register-page';

export const publicRoutes: Routes = [
  { path: '', component: HomePage },
  { path: 'login', component: LoginPage },
  { path: 'register', component: RegisterPage },
];
