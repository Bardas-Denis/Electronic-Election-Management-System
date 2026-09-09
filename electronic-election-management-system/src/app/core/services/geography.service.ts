import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  GeographicNode,
  CreateGeographicNodeRequest,
  RenameGeographicNodeRequest
} from '../models/geography.model';

// SYNC: api/geography (GeographyController) + api/admin/geography (GeographyAdminController)
// Error codes this service may propagate: labelNotFound, labelNameTakenUnderParent,
// labelHasChildren, labelHasUsersAndNoParent
@Injectable({ providedIn: 'root' })
export class GeographyService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/geography`;
  private adminBase = `${environment.apiUrl}/admin/geography`;

  // ── Reading: open to any signed-in user, because voters pick their region too ──

  /** Every country, ordered by name. */
  getCountries(): Observable<GeographicNode[]> {
    return this.http.get<GeographicNode[]>(`${this.base}/countries`);
  }

  /**
   * The direct children of one node — never the whole subtree, so no request carries
   * thousands of rows.
   */
  getChildren(parentId: string): Observable<GeographicNode[]> {
    return this.http.get<GeographicNode[]>(`${this.base}/${parentId}/children`);
  }

  // ── Writing: admin only ──────────────────────────────────────────────────

  /** Adds a node under an existing one. May fail with 'labelNameTakenUnderParent'. */
  createChild(request: CreateGeographicNodeRequest): Observable<GeographicNode> {
    return this.http.post<GeographicNode>(this.adminBase, request);
  }

  /** Renames a node, leaving its code and its place in the tree alone. */
  rename(id: string, request: RenameGeographicNodeRequest): Observable<GeographicNode> {
    return this.http.put<GeographicNode>(`${this.adminBase}/${id}`, request);
  }
}
