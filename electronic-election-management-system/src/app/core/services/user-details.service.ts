import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PersonalDetailsDto } from '../models/user-details.model';

// SYNC: api/me/details (UserDetailsController)
@Injectable({ providedIn: 'root' })
export class UserDetailsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/me/details`;

  /** Returns the current user's saved personal details, or null when none saved yet (204). */
  getMyDetails(): Observable<PersonalDetailsDto | null> {
    return this.http.get<PersonalDetailsDto | null>(this.baseUrl);
  }

  /** Creates or fully replaces the current user's personal details. */
  saveMyDetails(dto: PersonalDetailsDto): Observable<PersonalDetailsDto> {
    return this.http.put<PersonalDetailsDto>(this.baseUrl, dto);
  }
}
