import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ImageUploadResultDto } from '../models/voting.model';

// Ballot images are fetched through HttpClient rather than by pointing <img src> at the API,
// because a plain img request carries no Authorization header and the endpoint is authenticated.
// The blob becomes an object URL that the img element can use.
@Injectable({ providedIn: 'root' })
export class ElectionImageService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/images`;

  // Keyed by image id. Holding the observable rather than the URL also collapses the
  // concurrent requests a ballot full of pictures would otherwise fire for the same image.
  private readonly cache = new Map<string, Observable<string>>();
  private readonly objectUrls = new Map<string, string>();

  upload(file: File): Observable<ImageUploadResultDto> {
    const body = new FormData();
    body.append('file', file);
    return this.http.post<ImageUploadResultDto>(this.baseUrl, body);
  }

  resolve(imageId: string): Observable<string> {
    const cached = this.cache.get(imageId);
    if (cached) return cached;

    const stream = this.http
      .get(`${this.baseUrl}/${imageId}`, { responseType: 'blob' })
      .pipe(
        map((blob) => URL.createObjectURL(blob)),
        tap((url) => this.objectUrls.set(imageId, url)),
        shareReplay({ bufferSize: 1, refCount: false })
      );

    this.cache.set(imageId, stream);
    return stream;
  }

  // Object URLs are held by the browser until revoked, so dropping the cache alone would leak
  // the blobs for the rest of the session.
  clear(): void {
    this.objectUrls.forEach((url) => URL.revokeObjectURL(url));
    this.objectUrls.clear();
    this.cache.clear();
  }
}
