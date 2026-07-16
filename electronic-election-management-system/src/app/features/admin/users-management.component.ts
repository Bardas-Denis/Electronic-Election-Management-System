import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { from, concatMap, tap } from 'rxjs';
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
  private router = inject(Router);

  users = signal<UserDto[]>([]);
  isLoading = signal(true);
  errorMessage = signal<string | null>(null);
  isSaving = signal(false);

  // Staged role changes: userId → new role. Only populated when user edits a dropdown.
  // No HTTP request is made until "Save changes" is clicked.
  private pendingRoles = signal<Map<string, UserRole>>(new Map());

  // True when there is at least one unsaved role change.
  hasPendingChanges = computed(() => this.pendingRoles().size > 0);

  // Live guard: simulate what admin count would be after applying all pending changes.
  // Disables Save and shows a warning if the result is 0 admins.
  wouldLeaveZeroAdmins = computed(() => {
    const pending = this.pendingRoles();
    const adminCount = this.users().filter((u) => {
      const staged = pending.get(u.id);
      return (staged ?? u.role) === 'Admin';
    }).length;
    return adminCount === 0;
  });

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

  // Returns the role to display for a user: staged change if any, otherwise the persisted role.
  getDisplayRole(user: UserDto): UserRole {
    return this.pendingRoles().get(user.id) ?? user.role;
  }

  // Returns true if this row has a staged (unsaved) role change.
  hasPendingChange(user: UserDto): boolean {
    return this.pendingRoles().has(user.id);
  }

  // Stage a role change locally. If the user reverts to their current role, remove the entry.
  onRoleChange(user: UserDto, newRole: string): void {
    const role = newRole as UserRole;
    this.pendingRoles.update((map) => {
      const next = new Map(map);
      if (role === user.role) {
        next.delete(user.id); // reverted — no longer pending
      } else {
        next.set(user.id, role);
      }
      return next;
    });
  }

  // Send all staged changes sequentially (concatMap) to avoid the race condition where two
  // concurrent requests each see >=1 admin remaining and both succeed, leaving zero admins.
  saveChanges(): void {
    if (this.isSaving() || this.wouldLeaveZeroAdmins()) return;

    const entries = Array.from(this.pendingRoles().entries()); // [userId, newRole][]
    const currentUserId = this.authService.currentUser()?.userId;
    const isSelfRoleChange = entries.some(([userId]) => userId === currentUserId);

    this.isSaving.set(true);
    this.errorMessage.set(null);

    from(entries)
      .pipe(
        concatMap(([userId, role]) =>
          this.usersService.updateRole(userId, { role }).pipe(
            tap((updated) => {
              // Apply each successful update to the live users list immediately.
              this.users.update((list) =>
                list.map((u) => (u.id === updated.id ? updated : u))
              );
            })
          )
        )
      )
      .subscribe({
        error: (err) => {
          this.isSaving.set(false);
          this.pendingRoles.set(new Map()); // clear staged state
          this.loadUsers(); // reload to resync with server state
          this.errorMessage.set(
            err?.error?.message ?? 'Nu am putut salva modificarile de rol.'
          );
        },
        complete: () => {
          this.isSaving.set(false);
          this.pendingRoles.set(new Map());

          // Tokenul curent are încă rolul vechi. Pentru ca guard-urile și interfața
          // să se sincronizeze cu noul rol, forțăm o nouă autentificare.
          if (isSelfRoleChange) {
            this.authService.logout();
            this.router.navigate(['/login'], { queryParams: { reason: 'role-changed' } });
          }
        }
      });
  }

  // Discard all staged changes and reload to reset dropdowns to persisted state.
  discardChanges(): void {
    this.pendingRoles.set(new Map());
    this.loadUsers();
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

  // folosit in template ca sa blocheze un admin sa se stearga pe sine insusi
  isCurrentUser(user: UserDto): boolean {
    return this.authService.currentUser()?.userId === user.id;
  }
}