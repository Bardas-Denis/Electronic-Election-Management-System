import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Label, UserLabel, UserWithLabel, CreateLabelRequest, AssignLabelsRequest } from '../models/label.model';

// SYNC: api/admin/labels + api/admin/users/{id}/labels (LabelsController)
// Error codes this service may propagate: labelNameAlreadyExists, labelNotFound, resourceNotFound
@Injectable({ providedIn: 'root' })
export class LabelService {
  private http = inject(HttpClient);
  private adminBase = `${environment.apiUrl}/admin`;

  // ── Label management (admin) ─────────────────────────────────────────────

  /** Returns all labels. */
  getAllLabels(): Observable<Label[]> {
    return this.http.get<Label[]>(`${this.adminBase}/labels`);
  }

  /** Creates a new label. May fail with errorCode 'labelNameAlreadyExists'. */
  createLabel(request: CreateLabelRequest): Observable<Label> {
    return this.http.post<Label>(`${this.adminBase}/labels`, request);
  }

  /** Deletes a label and all its user assignments. */
  deleteLabel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.adminBase}/labels/${id}`);
  }

  /** Returns all users that have a given label assigned. */
  getUsersWithLabel(labelId: string): Observable<UserWithLabel[]> {
    return this.http.get<UserWithLabel[]>(`${this.adminBase}/labels/${labelId}/users`);
  }

  // ── User–label assignment (admin) ────────────────────────────────────────

  /** Returns all labels assigned to a specific user. */
  getUserLabels(userId: string): Observable<UserLabel[]> {
    return this.http.get<UserLabel[]>(`${this.adminBase}/users/${userId}/labels`);
  }

  // ── My Labels (User) ─────────────────────────────────────────────────────

  /** Returns all labels assigned to the currently authenticated user. */
  getMyLabels(): Observable<UserLabel[]> {
    return this.http.get<UserLabel[]>(`${environment.apiUrl}/me/labels`);
  }


  /**
   * Assigns one or more labels to a user (idempotent — already-assigned labels
   * are skipped server-side). Returns the full updated label list for that user.
   */
  assignLabelsToUser(userId: string, request: AssignLabelsRequest): Observable<UserLabel[]> {
    return this.http.post<UserLabel[]>(`${this.adminBase}/users/${userId}/labels`, request);
  }

  /** Removes a specific label from a user. */
  removeLabelFromUser(userId: string, labelId: string): Observable<void> {
    return this.http.delete<void>(`${this.adminBase}/users/${userId}/labels/${labelId}`);
  }
}
