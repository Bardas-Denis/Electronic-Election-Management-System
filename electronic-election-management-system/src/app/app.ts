import { Component, inject, signal, effect } from '@angular/core';
import { RouterOutlet, RouterLink, Router } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { NotificationsService } from './core/services/notifications.service';

// Root component: navbar + router outlet
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly authService = inject(AuthService);
  private router = inject(Router);
  private notifications = inject(NotificationsService);

  protected readonly title = signal('electronic-election-management-system'); // unused leftover

  constructor() {
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
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}