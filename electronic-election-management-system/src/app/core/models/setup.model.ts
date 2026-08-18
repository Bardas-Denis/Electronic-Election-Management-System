export type DbProvider = 'Sqlite' | 'Postgres';

export interface SetupRequest {
  provider: DbProvider;
  connectionString: string;
  adminEmail?: string;
  adminPassword?: string;
}

export interface SetupStatusResponse {
  configured: boolean;
}

export interface TestConnectionResponse {
  success: boolean;
  error?: string;
}

export interface SaveResponse {
  message: string;
}
