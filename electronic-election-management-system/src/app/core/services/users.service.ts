import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserDto, UpdateUserRoleRequest } from '../models/user.model';

// Admin-only endpoints - enforced server-side, not just by hiding the UI
@Injectable({ providedIn: 'root' })
export class UsersService {
  private baseUrl = `${environment.apiUrl}/users`;

  constructor(private http: HttpClient) {}

  getUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(this.baseUrl);
  }

  updateRole(userId: string, request: UpdateUserRoleRequest): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.baseUrl}/${userId}/role`, request);
  }

  deleteUser(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${userId}`);
  }
}