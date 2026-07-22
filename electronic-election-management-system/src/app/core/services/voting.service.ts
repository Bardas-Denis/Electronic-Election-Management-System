import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ElectionDto, CreateElectionRequest, CastVoteRequest, UserVoteDto } from '../models/voting.model';

// Thin HTTP wrapper - no business logic, just calls the backend
@Injectable({ providedIn: 'root' })
export class VotingService {
  private baseUrl = `${environment.apiUrl}/voting`;

  constructor(private http: HttpClient) {}

  getElections(): Observable<ElectionDto[]> {
    return this.http.get<ElectionDto[]>(`${this.baseUrl}/elections`);
  }

  getMyElections(): Observable<ElectionDto[]> {
    return this.http.get<ElectionDto[]>(`${this.baseUrl}/elections/mine`);
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

  getMyVote(electionId: string): Observable<UserVoteDto> {
    return this.http.get<UserVoteDto>(`${this.baseUrl}/votes/${electionId}/me`);
  }

  updateMyVote(request: CastVoteRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/votes/${request.electionId}`, request);
  }

  deleteMyVote(electionId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/votes/${electionId}`);
  }
}