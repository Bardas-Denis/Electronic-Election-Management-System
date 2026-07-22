import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  ViewChild
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements AfterViewInit, OnDestroy {
  private authService = inject(AuthService);
  private router = inject(Router);
  private revealObserver?: IntersectionObserver;

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
    try {
      const video = this.heroVideo?.nativeElement;
      const section = this.heroSection?.nativeElement;

      if (video && section) {
        const showVideo = () => section.classList.remove('no-video');
        video.addEventListener('canplay', showVideo, { once: true });
        video.addEventListener('playing', showVideo, { once: true });

        const playPromise = video.play();
        if (playPromise && typeof playPromise.then === 'function') {
          playPromise.then(showVideo).catch(() => undefined);
        }
      }
    } catch {
      // Keep the editorial gradient fallback when video playback is unavailable.
    }

    this.setupScrollReveals();
  }

  ngOnDestroy(): void {
    this.revealObserver?.disconnect();
  }

  private setupScrollReveals(): void {
    const elements = document.querySelectorAll<HTMLElement>('[data-reveal]');
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!('IntersectionObserver' in window) || reduceMotion) {
      elements.forEach((element) => element.classList.add('is-visible'));
      return;
    }

    this.revealObserver = new IntersectionObserver(
      (entries, observer) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        });
      },
      { threshold: 0.16, rootMargin: '0px 0px -8% 0px' }
    );

    elements.forEach((element) => this.revealObserver?.observe(element));
  }
}
