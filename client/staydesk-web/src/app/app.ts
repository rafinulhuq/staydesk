import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from './core/auth/auth.service';
import { LoadingService } from './core/http/loading.service';
import { Language, LanguageService } from './core/i18n/language.service';

@Component({
  selector: 'sd-root',
  imports: [
    RouterOutlet,
    RouterLink,
    TranslatePipe,
    MatToolbarModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatProgressBarModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly loading = inject(LoadingService);
  protected readonly language = inject(LanguageService);

  protected switchLanguage(language: Language): void {
    this.language.use(language);
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/'));
  }
}
