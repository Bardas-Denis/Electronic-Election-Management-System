import { Component, inject, signal } from '@angular/core';
import { RouterOutlet, RouterLink, Router } from '@angular/router';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly authService = inject(AuthService);
  private router = inject(Router);

  protected readonly title = signal('electronic-election-management-system');

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
