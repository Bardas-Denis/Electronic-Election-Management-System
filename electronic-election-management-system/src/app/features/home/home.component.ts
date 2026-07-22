import { Component, inject, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
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
export class HomeComponent implements AfterViewInit {
  private authService = inject(AuthService);
  private router = inject(Router);

  readonly currentYear = new Date().getFullYear();

  @ViewChild('heroVideo', { static: false })
  private heroVideo?: ElementRef<HTMLVideoElement>;

  @ViewChild('heroSection', { static: false })
  private heroSection?: ElementRef<HTMLElement>;

  constructor() {
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/elections'], { replaceUrl: true });
    }
  }

  ngAfterViewInit(): void {
    // Attempt to play the background video. If playback starts, remove the
    // fallback class so the video is visible; otherwise keep the gradient.
    try {
      const video = this.heroVideo?.nativeElement;
      const section = this.heroSection?.nativeElement;
      if (!video || !section) return;

      const showVideo = () => {
        section.classList.remove('no-video');
      };

      const failVideo = () => {
        // keep the no-video class (gradient fallback)
      };

      video.addEventListener('canplay', showVideo, { once: true });
      video.addEventListener('playing', showVideo, { once: true });
      video.addEventListener('error', failVideo, { once: true });

      // Some browsers block autoplay even for muted videos; try to play and
      // fallback gracefully.
      const playPromise = video.play();
      if (playPromise && typeof playPromise.then === 'function') {
        playPromise.then(() => showVideo()).catch(() => failVideo());
      }
    } catch (e) {
      // ignore — leave the gradient in place
    }
  }
}
