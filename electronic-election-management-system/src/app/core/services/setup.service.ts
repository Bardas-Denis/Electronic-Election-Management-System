import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SetupRequest,
  SetupStatusResponse,
  TestConnectionResponse,
  SaveResponse,
  AvailableProvidersResponse
} from '../models/setup.model';

const SETUP_BASE = `${environment.apiUrl}/setup`;

@Injectable({ providedIn: 'root' })
export class SetupService {
  private readonly http = inject(HttpClient);

  /** GET /api/setup/status — always succeeds even in unconfigured mode. */
  getStatus(): Observable<SetupStatusResponse> {
    return this.http.get<SetupStatusResponse>(`${SETUP_BASE}/status`);
  }

  /**
   * GET /api/setup/available-providers — which providers to offer as choices.
   * Deployment-time setting (appsettings.json), separate from the saved config.
   */
  getAvailableProviders(): Observable<AvailableProvidersResponse> {
    return this.http.get<AvailableProvidersResponse>(`${SETUP_BASE}/available-providers`);
  }

  /**
   * POST /api/setup/test-connection — probes the connection without writes.
   * Returns { success: true } or { success: false, error: '...' }.
   * Safe to call when already configured.
   */
  testConnection(req: SetupRequest): Observable<TestConnectionResponse> {
    return this.http.post<TestConnectionResponse>(`${SETUP_BASE}/test-connection`, req);
  }

  /**
   * POST /api/setup/save — validates, writes config, migrates, then stops the process.
   * Returns 409 if already configured.
   */
  save(req: SetupRequest): Observable<SaveResponse> {
    return this.http.post<SaveResponse>(`${SETUP_BASE}/save`, req);
  }
}