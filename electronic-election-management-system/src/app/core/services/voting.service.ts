import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ElectionDto, CreateElectionRequest, CastVoteRequest } from '../models/voting.model';

@Injectable({ providedIn: 'root' })
export class VotingService {
  private baseUrl = `${environment.apiUrl}/voting`;

  constructor(private http: HttpClient) {}

  getElections(): Observable<ElectionDto[]> {
    return this.http.get<ElectionDto[]>(`${this.baseUrl}/elections`);
  }

  getElectionById(id: string): Observable<ElectionDto> {
    return this.http.get<ElectionDto>(`${this.baseUrl}/elections/${id}`);
  }

  createElection(request: CreateElectionRequest): Observable<ElectionDto> {
    return this.http.post<ElectionDto>(`${this.baseUrl}/elections`, request);
  }

  updateElection(id: string, request: CreateElectionRequest): Observable<ElectionDto> {
    return this.http.put<ElectionDto>(`${this.baseUrl}/elections/${id}`, request);
  }

  deleteElection(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/elections/${id}`);
  }

  castVote(request: CastVoteRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/votes`, request);
  }
}
