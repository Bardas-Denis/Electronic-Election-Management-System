import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { ElectionResultsDto } from '../models/results.model';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class ResultsService {
  private hubConnection?: signalR.HubConnection;

  // rezultatele curente, actualizate live prin SignalR
  liveResults = signal<ElectionResultsDto | null>(null);

  constructor(private http: HttpClient, private authService: AuthService) {}

  getResultsSnapshot(electionId: string) {
    return this.http.get<ElectionResultsDto>(
      `${environment.apiUrl}/results/${electionId}`
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
      .catch((err: unknown) => console.error('Eroare la conectarea SignalR:', err));

    // Numele evenimentului trebuie sa corespunda cu ce trimite ResultsHub din backend
    this.hubConnection.on('ResultsUpdated', (results: ElectionResultsDto) => {
      this.liveResults.set(results);
    });
  }

  disconnect(): void {
    this.hubConnection?.stop();
    this.liveResults.set(null);
  }
}