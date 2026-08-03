import { Component, inject, signal, effect, HostListener} from '@angular/core';
import { RouterOutlet, RouterLink, Router, RouterLinkActive } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from './core/services/auth.service';
import { NotificationsService } from './core/services/notifications.service';
import { NotificationsComponent } from './core/components/notifications/notifications.component';

const VALID_LANGS = ['ro', 'en'] as const;
type Lang = typeof VALID_LANGS[number];

// Root component: navbar + router outlet
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe, NotificationsComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly authService = inject(AuthService);
  readonly translate = inject(TranslateService);
  private router = inject(Router);
  private notifications = inject(NotificationsService);

  protected readonly title = signal('electronic-election-management-system'); // unused leftover

  // Current theme for UI (light | dark). Kept as a simple property so templates can read it.
  public currentTheme: 'light' | 'dark' = 'light';
  
  isUserMenuOpen = signal(false);

  get isHomePage(): boolean {
    return this.router.url === '/' || this.router.url.startsWith('/?');
  }

  constructor() {
    // Restore the user's last-chosen language from localStorage before any rendering.
    const stored = localStorage.getItem('preferredLang');
    if (stored && (VALID_LANGS as readonly string[]).includes(stored)) {
      this.translate.use(stored as Lang);
    }

    // Mirror the auth signal: open the notifications hub for the whole authenticated
    // session, close it immediately on logout (or when the token is cleared by the
    // 401 interceptor). No manual tracking needed — effect() reacts to the signal.
    effect(() => {
      if (this.authService.currentUser() !== null) {
        this.notifications.connect();
      } else {
        this.notifications.disconnect();
      }
    });

    // Initialize theme from localStorage (if present) and apply it.
    try {
      const saved = localStorage.getItem('theme');
      const init = saved === 'dark' ? 'dark' : 'light';
      this.currentTheme = init;
      this.applyTheme(init);
    } catch (e) {
      // localStorage may be unavailable in some environments; default to light
      this.currentTheme = 'light';
    }
  }

  toggleTheme(): void {
    const next: 'light' | 'dark' = this.currentTheme === 'dark' ? 'light' : 'dark';
    this.currentTheme = next;
    try { localStorage.setItem('theme', next); } catch { }
    this.applyTheme(next);
  }

  private applyTheme(theme: 'light' | 'dark'): void {
    if (typeof document !== 'undefined' && document.documentElement) {
      if (theme === 'dark') {
        document.documentElement.setAttribute('data-theme', 'dark');
      } else {
        document.documentElement.removeAttribute('data-theme');
      }
    }
  }

  logout(): void {
    this.authService.logout();
    // Navigate to the first (root) page after logout
    this.router.navigate(['/']);
  }

  /** Switch UI language and persist the choice across page reloads. */
  switchLanguage(lang: Lang): void {
    this.translate.use(lang);
    localStorage.setItem('preferredLang', lang);
  }

  toggleUserMenu(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.isUserMenuOpen.update(v => !v);
  }

  closeUserMenu(): void {
    this.isUserMenuOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (this.isUserMenuOpen() && !target.closest('.user-menu-container')) {
      this.closeUserMenu();
    }
  }
}
