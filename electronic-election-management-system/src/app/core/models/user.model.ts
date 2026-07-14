export type UserRole = 'Admin' | 'ElectionManager' | 'Voter';

// Used in the admin users management page
export interface UserDto {
  id: string;
  email: string;
  role: UserRole;
  createdAt: string;
}

export interface UpdateUserRoleRequest {
  role: UserRole;
}