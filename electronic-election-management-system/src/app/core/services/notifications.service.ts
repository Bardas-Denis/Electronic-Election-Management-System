import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private hubConnection?: signalR.HubConnection;

  private authService = inject(AuthService);
  private router = inject(Router);

  // Starts the hub connection and registers the RoleChanged handler.
  // Called once when the user logs in; lives for the entire authenticated session.
  connect(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.notificationsHubUrl, {
        accessTokenFactory: () => this.authService.getToken() ?? '' // JWT for SignalR auth
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .catch((err: unknown) => console.error('NotificationsHub connection error:', err));

    // SignalR does not go through HttpClient, so the auth interceptor never sees
    // these failures — handle expired/invalid tokens manually on close.
    this.hubConnection.onclose(() => {
      if (!this.authService.isLoggedIn()) {
        this.authService.logout();
        if (!this.router.url.startsWith('/login')) {
          this.router.navigate(['/login'], { queryParams: { reason: 'session-expired' } });
        }
      }
    });

    // Server pushes this event immediately after an admin changes the user's role.
    // The SecurityStamp mechanism already enforces the logout via a 401 on the next
    // request, so this is a UX improvement only — it surfaces the change instantly
    // rather than waiting for the next click.
    this.hubConnection.on('RoleChanged', () => {
      this.authService.logout();
      this.router.navigate(['/login'], { queryParams: { reason: 'role-changed' } });
    });
  }

  // Stops the connection; called when the user logs out.
  disconnect(): void {
    this.hubConnection?.stop();
    this.hubConnection = undefined;
  }
}
