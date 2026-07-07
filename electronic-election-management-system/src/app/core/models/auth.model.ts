export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  userId: string;
  email: string;
  role: 'Admin' | 'Voter';
  expiresAt: string;
}

export interface CurrentUser {
  userId: string;
  email: string;
  role: 'Admin' | 'Voter';
}
