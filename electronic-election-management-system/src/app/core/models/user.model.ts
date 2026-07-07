export type UserRole = 'Admin' | 'Voter';

export interface UserDto {
  id: string;
  email: string;
  role: UserRole;
  createdAt: string;
}

export interface UpdateUserRoleRequest {
  role: UserRole;
}
