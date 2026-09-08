import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
  computed,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { ElectionImageService } from '../../core/services/election-image.service';
import { ImageUploadResultDto } from '../../core/models/voting.model';

const OUTPUT_SIZE = 1024; // matches the backend's ValidationRules.ImageMaxDimension
const MIN_ZOOM = 1; // 1 = the widest possible selection (see computeMaxBoxSize)
const MAX_ZOOM = 4;
// Half the zoom slider's knob. Must match the knob size in the .scss, since the
// filled part of the track is drawn to the knob's centre (see zoomTrackBackground).
const ZOOM_KNOB_RADIUS_PX = 11;

/** Pure, unit-testable geometry helpers - kept free of signals/DOM so they can be
 *  tested without mounting the component. */

// Keeps a box's top-left within [0, naturalSize - boxSize]. If the box is bigger
// than the photo on that axis, it's pinned to 0 rather than centered - callers
// never actually hit that case since computeMaxBoxSize caps boxSize at naturalSize.
export function clampBoxPosition(pos: number, boxSize: number, naturalSize: number): number {
  const max = Math.max(0, naturalSize - boxSize);
  return Math.min(Math.max(pos, 0), max);
}

// "Contain" fit for a square stage: scales by the photo's longer edge so the
// whole photo is visible, letterboxed on the shorter axis.
export function computeDisplayScale(stageSize: number, naturalWidth: number, naturalHeight: number): number {
  const longestEdge = Math.max(naturalWidth, naturalHeight);
  return longestEdge > 0 ? stageSize / longestEdge : 1;
}

// The widest the selection square can be: the output resolution, unless the
// photo itself is smaller than that on its shorter edge.
export function computeMaxBoxSize(naturalWidth: number, naturalHeight: number, outputSize: number): number {
  return Math.min(outputSize, naturalWidth, naturalHeight);
}

// Instagram-style crop: the whole photo stays visible and in place; a square
// selection is moved (drag) and resized (wheel/slider, keeping its center) over
// it, dimming everything outside via a giant box-shadow. On confirm, the exact
// selected pixels are drawn to a canvas and uploaded through the existing images
// endpoint - this component owns that HTTP call itself, mirroring how
// CreateScoringSchemeModalComponent owns its own save call.
@Component({
  selector: 'app-crop-image-modal',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './crop-image-modal.component.html',
  styleUrl: './crop-image-modal.component.scss'
})
export class CropImageModalComponent implements OnInit, OnDestroy {
  @Input({ required: true }) file!: File;
  @Output() uploaded = new EventEmitter<ImageUploadResultDto>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly images = inject(ElectionImageService);

  @ViewChild('stage') private stageRef?: ElementRef<HTMLDivElement>;
  @ViewChild('photo') private photoRef?: ElementRef<HTMLImageElement>;

  readonly MIN_ZOOM = MIN_ZOOM;
  readonly MAX_ZOOM = MAX_ZOOM;

  readonly objectUrl = signal<string | null>(null);
  readonly isUploading = signal(false);
  readonly errorMessageKey = signal<string | null>(null);

  // The photo's own on-screen size (contain-fit within the square stage).
  readonly displayScale = signal(1);
  readonly displayedWidth = signal(0);
  readonly displayedHeight = signal(0);

  // The selection square, entirely in the photo's natural pixel coordinates -
  // this is also exactly the source rectangle used at export time.
  readonly zoom = signal(MIN_ZOOM);
  readonly boxSize = signal(0);
  readonly boxX = signal(0);
  readonly boxY = signal(0);

  // Derived on-screen placement of the selection square, for the template.
  readonly boxLeftPx = computed(
    () => (this.stageSize - this.displayedWidth()) / 2 + this.boxX() * this.displayScale()
  );
  readonly boxTopPx = computed(
    () => (this.stageSize - this.displayedHeight()) / 2 + this.boxY() * this.displayScale()
  );
  readonly boxSizePx = computed(() => this.boxSize() * this.displayScale());

  // The custom track has no built-in "filled" portion the way accent-color gave
  // us for free, so it's painted by hand as a gradient.
  //
  // The split can't be a plain percentage of the track: the knob's centre only
  // travels between one radius in from each end, so a plain percentage runs
  // ahead of the knob as the value approaches the maximum, showing a sliver of
  // fill past it. Expressing the split in the knob's own travel instead keeps
  // the fill ending exactly under the knob's centre at every position.
  readonly zoomTrackBackground = computed(() => {
    const fraction = (this.zoom() - MIN_ZOOM) / (MAX_ZOOM - MIN_ZOOM);
    const split =
      `calc(${ZOOM_KNOB_RADIUS_PX}px + ${fraction} * (100% - ${ZOOM_KNOB_RADIUS_PX * 2}px))`;
    return (
      `linear-gradient(to right, var(--accent) 0, var(--accent) ${split}, ` +
      `var(--border-color) ${split}, var(--border-color) 100%)`
    );
  });

  private naturalWidth = 0;
  private naturalHeight = 0;
  private stageSize = 0;

  private dragPointerId: number | null = null;
  private dragOrigin = { x: 0, y: 0, boxX: 0, boxY: 0 };

  ngOnInit(): void {
    this.objectUrl.set(URL.createObjectURL(this.file));
  }

  ngOnDestroy(): void {
    const url = this.objectUrl();
    if (url) URL.revokeObjectURL(url);
  }

  // Runs once the picked photo has actually decoded - only then do we know its
  // natural size, and only then do we measure the stage (once, not on every
  // pointer event, so dragging never triggers a synchronous layout read).
  onPhotoLoad(event: Event): void {
    const img = event.target as HTMLImageElement;
    this.naturalWidth = img.naturalWidth;
    this.naturalHeight = img.naturalHeight;
    this.stageSize = this.stageRef?.nativeElement.clientWidth ?? 0;

    const scale = computeDisplayScale(this.stageSize, this.naturalWidth, this.naturalHeight);
    this.displayScale.set(scale);
    this.displayedWidth.set(this.naturalWidth * scale);
    this.displayedHeight.set(this.naturalHeight * scale);

    this.zoom.set(MIN_ZOOM);
    const maxBox = computeMaxBoxSize(this.naturalWidth, this.naturalHeight, OUTPUT_SIZE);
    this.boxSize.set(maxBox);
    this.boxX.set((this.naturalWidth - maxBox) / 2);
    this.boxY.set((this.naturalHeight - maxBox) / 2);
  }

  // Resizes the selection around its current center, then re-clamps position -
  // zooming back out shrinks how much the box could have moved, so a
  // previously-valid position can become out of range purely from the resize.
  private applyZoom(nextZoom: number): void {
    const clamped = Math.min(Math.max(nextZoom, MIN_ZOOM), MAX_ZOOM);
    const maxBox = computeMaxBoxSize(this.naturalWidth, this.naturalHeight, OUTPUT_SIZE);
    const newSize = maxBox / clamped;
    const centerX = this.boxX() + this.boxSize() / 2;
    const centerY = this.boxY() + this.boxSize() / 2;

    this.zoom.set(clamped);
    this.boxSize.set(newSize);
    this.boxX.set(clampBoxPosition(centerX - newSize / 2, newSize, this.naturalWidth));
    this.boxY.set(clampBoxPosition(centerY - newSize / 2, newSize, this.naturalHeight));
  }

  onZoomSlider(event: Event): void {
    this.applyZoom(Number((event.target as HTMLInputElement).value));
  }

  onWheel(event: WheelEvent): void {
    event.preventDefault();
    this.applyZoom(this.zoom() * Math.exp(-event.deltaY * 0.0015));
  }

  onPointerDown(event: PointerEvent): void {
    if (this.isUploading()) return;
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
    this.dragPointerId = event.pointerId;
    this.dragOrigin = { x: event.clientX, y: event.clientY, boxX: this.boxX(), boxY: this.boxY() };
  }

  onPointerMove(event: PointerEvent): void {
    if (this.dragPointerId !== event.pointerId) return;
    // Screen-px pointer movement -> natural-px box movement, via the same scale
    // that maps the photo's natural size onto its on-screen size.
    const dx = (event.clientX - this.dragOrigin.x) / this.displayScale();
    const dy = (event.clientY - this.dragOrigin.y) / this.displayScale();
    this.boxX.set(clampBoxPosition(this.dragOrigin.boxX + dx, this.boxSize(), this.naturalWidth));
    this.boxY.set(clampBoxPosition(this.dragOrigin.boxY + dy, this.boxSize(), this.naturalHeight));
  }

  onPointerUp(event: PointerEvent): void {
    if (this.dragPointerId !== event.pointerId) return;
    this.dragPointerId = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.onCancel();
  }

  onCancel(): void {
    // Ignored while uploading so a successful upload can never be silently lost.
    if (this.isUploading()) return;
    this.cancelled.emit();
  }

  onConfirm(): void {
    if (this.isUploading() || !this.photoRef) return;
    this.errorMessageKey.set(null);
    this.isUploading.set(true);

    const canvas = document.createElement('canvas');
    canvas.width = OUTPUT_SIZE;
    canvas.height = OUTPUT_SIZE;
    const ctx = canvas.getContext('2d');
    if (!ctx) {
      this.isUploading.set(false);
      this.errorMessageKey.set('elections.cropExportFailed');
      return;
    }

    // drawImage samples the <img>'s decoded natural pixels regardless of its
    // (smaller) on-screen CSS size, so boxX/boxY/boxSize - already in natural
    // coordinates - are exactly the source rectangle, no extra scaling needed.
    const size = this.boxSize();
    ctx.drawImage(this.photoRef.nativeElement, this.boxX(), this.boxY(), size, size, 0, 0, OUTPUT_SIZE, OUTPUT_SIZE);

    canvas.toBlob(
      (blob) => {
        if (!blob) {
          this.isUploading.set(false);
          this.errorMessageKey.set('elections.cropExportFailed');
          return;
        }
        // A canvas blob has no filename, and the upload endpoint reads one from the
        // multipart body - wrap it before handing it to the existing upload() call.
        const croppedFile = new File([blob], 'crop.webp', { type: blob.type });
        this.images.upload(croppedFile).subscribe({
          next: (result) => {
            this.isUploading.set(false);
            this.uploaded.emit(result);
          },
          error: () => {
            this.isUploading.set(false);
            this.errorMessageKey.set('elections.imageUploadFailed');
          }
        });
      },
      'image/webp',
      0.92
    );
  }
}
