import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { ElectionResultsDto, OptionVotersDto, TextAnswerAuthorDto } from '../models/results.model';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class ResultsService {
  private hubConnection?: signalR.HubConnection;

  // rezultatele curente, actualizate live prin SignalR
  liveResults = signal<ElectionResultsDto | null>(null);

  constructor(private http: HttpClient, private authService: AuthService, private router: Router) {}

  getResultsSnapshot(electionId: string) {
    return this.http.get<ElectionResultsDto>(
      `${environment.apiUrl}/results/${electionId}`
    );
  }

  // Who voted for what, grouped by option. Deliberately a separate call from the
  // results snapshot: that one is broadcast over SignalR to every subscriber, and
  // voter identities have no business travelling with it. The server refuses this
  // for anonymous elections and for anyone who is neither an admin nor the
  // election's creator, so the answer is authoritative rather than advisory.
  // Who wrote each typed answer on one question - a FreeText question's answers,
  // or a Choice question's "Other" ones. Same access rules as getVoters: refused
  // for anonymous elections and for anyone who is neither an admin nor the
  // election's creator.
  getTextAnswerAuthors(electionId: string, questionId: string) {
    return this.http.get<TextAnswerAuthorDto[]>(
      `${environment.apiUrl}/results/${electionId}/questions/${questionId}/text-answers`
    );
  }

  getVoters(electionId: string, questionId?: string) {
    const query = questionId ? `?questionId=${encodeURIComponent(questionId)}` : '';
    return this.http.get<OptionVotersDto[]>(
      `${environment.apiUrl}/results/${electionId}/voters${query}`
    );
  }

  // Se conecteaza la hub-ul SignalR si se aboneaza la update-urile
  // pentru o anumita alegere. Apeleaza asta cand utilizatorul deschide dashboard-ul.
  connectToLiveResults(electionId: string): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.signalRUrl, {
        accessTokenFactory: () => this.authService.getToken() ?? '' // JWT pentru autentificare SignalR
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => this.hubConnection?.invoke('JoinElectionGroup', electionId))
      .catch((err: unknown) => {
        console.error('Eroare la conectarea SignalR:', err);
        this.redirectIfSessionExpired();
      });

    // SignalR doesn't route through HttpClient, so authErrorInterceptor never
    // sees these failures - an expired/invalid token here has to be checked
    // manually whenever the connection drops or fails to (re)negotiate.
    this.hubConnection.onclose(() => this.redirectIfSessionExpired());

    // Numele evenimentului trebuie sa corespunda cu ce trimite ResultsHub din backend
    this.hubConnection.on('ResultsUpdated', (results: ElectionResultsDto) => {
      this.liveResults.set(results);
    });
  }

  disconnect(): void {
    this.hubConnection?.stop();
    this.liveResults.set(null);
  }

  // If the token has expired, the hub rejects/drops the connection - treat
  // that the same way the HTTP interceptor treats a 401.
  private redirectIfSessionExpired(): void {
    if (!this.authService.isLoggedIn()) {
      this.authService.logout();
      if (!this.router.url.startsWith('/login')) {
        this.router.navigate(['/login'], { queryParams: { reason: 'session-expired' } });
      }
    }
  }
}