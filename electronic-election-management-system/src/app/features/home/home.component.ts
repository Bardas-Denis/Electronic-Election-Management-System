import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

// Public marketing / front page. Logged-in users are bounced straight to
// the elections list - this page is only meant to be seen by guests.
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  readonly currentYear = new Date().getFullYear();

  constructor() {
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/elections']);
    }
  }
}
