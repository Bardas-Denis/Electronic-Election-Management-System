import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GeographicNode } from '../models/geography.model';

// SYNC: api/geography (GeographyController)
/**
 * Reads the geographic label tree one level at a time. Nothing here loads the whole tree: it
 * holds roughly 4,700 nodes, and a picker only ever needs the level it is showing.
 */
@Injectable({ providedIn: 'root' })
export class GeographyService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/geography`;

  /** Every country, ordered by name. */
  getCountries(): Observable<GeographicNode[]> {
    return this.http.get<GeographicNode[]>(`${this.base}/countries`);
  }

  /** The direct children of one node — a country's subdivisions, a subdivision's localities. */
  getChildren(nodeId: string): Observable<GeographicNode[]> {
    return this.http.get<GeographicNode[]>(`${this.base}/${nodeId}/children`);
  }
}
