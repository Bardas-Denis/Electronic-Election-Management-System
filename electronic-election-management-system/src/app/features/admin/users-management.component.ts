import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsersService } from '../../core/services/users.service';
import { AuthService } from '../../core/services/auth.service';
import { UserDto, UserRole } from '../../core/models/user.model';

// Panou de administrare: "gestionarea utilizatorilor si rolurilor" (Etapa 2 din PDF).
@Component({
  selector: 'app-users-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './users-management.component.html',
  styleUrl: './users-management.component.scss'
})
export class UsersManagementComponent implements OnInit {
  private usersService = inject(UsersService);
  readonly authService = inject(AuthService);

  users = signal<UserDto[]>([]);
  isLoading = signal(true);
  errorMessage = signal<string | null>(null);

  // tine minte ce update de rol e in curs, ca sa dezactivam doar select-ul respectiv
  savingUserId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.usersService.getUsers().subscribe({
      next: (data) => {
        this.users.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Nu am putut incarca lista de utilizatori.');
        this.isLoading.set(false);
      }
    });
  }

  onRoleChange(user: UserDto, newRole: string): void {
    this.savingUserId.set(user.id);

    this.usersService.updateRole(user.id, { role: newRole as UserRole }).subscribe({
      next: (updated) => {
        this.users.update((list) =>
          list.map((u) => (u.id === updated.id ? updated : u))
        );
        this.savingUserId.set(null);
      },
      error: (err) => {
        alert(err?.error?.message ?? 'Nu am putut schimba rolul acestui utilizator.');
        this.savingUserId.set(null);
        this.loadUsers(); // resincronizam select-ul cu starea reala din backend
      }
    });
  }

  deleteUser(user: UserDto): void {
    if (!confirm(`Stergi utilizatorul ${user.email}? Aceasta actiune nu poate fi anulata.`)) {
      return;
    }

    this.usersService.deleteUser(user.id).subscribe({
      next: () => this.users.update((list) => list.filter((u) => u.id !== user.id)),
      error: (err) => alert(err?.error?.message ?? 'Nu am putut sterge acest utilizator.')
    });
  }

  isCurrentUser(user: UserDto): boolean {
    return this.authService.currentUser()?.userId === user.id;
  }
}
