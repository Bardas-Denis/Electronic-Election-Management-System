import { Directive, DestroyRef, ElementRef, effect, inject, input } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ElectionImageService } from '../services/election-image.service';

// Sets an img's src from a ballot image id: <img [appElectionImage]="option.imageId" />
// A null or missing id leaves the element without a src, so the caller can bind unconditionally.
@Directive({ selector: 'img[appElectionImage]' })
export class ElectionImageDirective {
  private readonly images = inject(ElectionImageService);
  private readonly element = inject<ElementRef<HTMLImageElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly imageId = input<string | null | undefined>(undefined, { alias: 'appElectionImage' });

  constructor() {
    effect(() => {
      const imageId = this.imageId();
      const image = this.element.nativeElement;

      if (!imageId) {
        image.removeAttribute('src');
        return;
      }

      this.images
        .resolve(imageId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (url) => (image.src = url),
          // A deleted or inaccessible image must not leave a broken icon on the ballot.
          error: () => image.removeAttribute('src')
        });
    });
  }
}
