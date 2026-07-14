export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

// Response from login/register - contains everything needed to start a session
export interface AuthResponse {
  token: string;
  userId: string;
  email: string;
  role: 'Admin' | 'ElectionManager' | 'Voter';
  expiresAt: string;
}

// Decoded from the JWT payload, used for UI display (see auth.service.ts)
export interface CurrentUser {
  userId: string;
  email: string;
  role: 'Admin' | 'ElectionManager' | 'Voter';
}