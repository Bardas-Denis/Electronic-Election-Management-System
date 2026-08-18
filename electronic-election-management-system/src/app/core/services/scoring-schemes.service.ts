import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ScoringSchemeDto, CreateScoringSchemeDto } from '../models/scoring-schemes.model';

@Injectable({
  providedIn: 'root'
})
export class ScoringSchemesService {
  private baseUrl = `${environment.apiUrl}/scoring-schemes`;

  constructor(private http: HttpClient) {}

  getSchemes(): Observable<ScoringSchemeDto[]> {
    return this.http.get<ScoringSchemeDto[]>(this.baseUrl);
  }

  createScheme(request: CreateScoringSchemeDto): Observable<ScoringSchemeDto> {
    return this.http.post<ScoringSchemeDto>(this.baseUrl, request);
  }
}
