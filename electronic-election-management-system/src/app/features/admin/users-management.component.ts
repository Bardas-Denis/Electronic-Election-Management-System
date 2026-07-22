import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { from, concatMap, tap } from 'rxjs';
import { UsersService } from '../../core/services/users.service';
import { AuthService } from '../../core/services/auth.service';
import { UserDto, UserRole } from '../../core/models/user.model';

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
  isBulkActionSaving = signal(false);

  // Semnale pentru filtrare și paginare
  selectedRoleFilter = signal<string>('ALL');
  searchQuery = signal<string>('');
  currentPage = signal<number>(1);
  pageSize = 50;

  // Semnal pentru selecția multiplă (stochează ID-urile utilizatorilor bifați)
  selectedUserIds = signal<Set<string>>(new Set());

  // Computed: filtrează utilizatorii
  filteredUsers = computed(() => {
    const roleFilter = this.selectedRoleFilter();
    const query = this.searchQuery().toLowerCase().trim();
    const allUsers = this.users();

    return allUsers.filter((user) => {
      const matchesRole = roleFilter === 'ALL' || user.role === roleFilter;
      const matchesEmail = !query || user.email.toLowerCase().includes(query);
      return matchesRole && matchesEmail;
    });
  });

  // Computed: numărul total de pagini
  totalPages = computed(() => {
    const total = this.filteredUsers().length;
    return Math.ceil(total / this.pageSize) || 1;
  });

  // Computed: taie lista doar pentru pagina curentă (câte 50)
  pagedUsers = computed(() => {
    const filtered = this.filteredUsers();
    const page = this.currentPage();
    const start = (page - 1) * this.pageSize;
    return filtered.slice(start, start + this.pageSize);
  });

  // Computed: verifică dacă toți utilizatorii din pagina curentă sunt selectați
  isAllSelected = computed(() => {
    const paged = this.pagedUsers();
    if (paged.length === 0) return false;
    return paged.every(u => this.selectedUserIds().has(u.id));
  });

  private pendingRoles = signal<Map<string, UserRole>>(new Map());

  hasPendingChanges = computed(() => this.pendingRoles().size > 0);

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

  setRoleFilter(role: string): void {
    this.selectedRoleFilter.set(role);
    this.currentPage.set(1);
    this.selectedUserIds.set(new Set());
  }

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
    this.currentPage.set(1);
    this.selectedUserIds.set(new Set());
  }

  onSearchClear(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.value) {
      this.searchQuery.set('');
      this.currentPage.set(1);
      this.selectedUserIds.set(new Set());
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
    }
  }

  // Metode pentru selecția multiplă
  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedUserIds.update(set => {
      const next = new Set(set);
      this.pagedUsers().forEach(u => {
        if (checked) {
          next.add(u.id);
        } else {
          next.delete(u.id);
        }
      });
      return next;
    });
  }

  toggleUserSelection(userId: string): void {
    this.selectedUserIds.update(set => {
      const next = new Set(set);
      if (next.has(userId)) {
        next.delete(userId);
      } else {
        next.add(userId);
      }
      return next;
    });
  }

  isUserSelected(userId: string): boolean {
    return this.selectedUserIds().has(userId);
  }

  // Acțiune în masă: Ștergerea directă a utilizatorilor selectați
  deleteSelectedUsers(): void {
    const currentUserId = this.authService.currentUser()?.userId;
    const idsToDelete = Array.from(this.selectedUserIds()).filter(id => id !== currentUserId);

    if (idsToDelete.length === 0) return;

    if (!confirm(`Sigur vrei să ștergi cei ${idsToDelete.length} utilizatori selectați?`)) {
      return;
    }

    this.isBulkActionSaving.set(true);

    from(idsToDelete)
      .pipe(
        concatMap(id => this.usersService.deleteUser(id))
      )
      .subscribe({
        complete: () => {
          this.users.update(list => list.filter(u => !idsToDelete.includes(u.id)));
          this.selectedUserIds.set(new Set());
          this.isBulkActionSaving.set(false);
        },
        error: (err) => {
          this.isBulkActionSaving.set(false);
          alert(err?.error?.message ?? 'A apărut o eroare la ștergerea în masă.');
          this.loadUsers();
        }
      });
  }

  // Acțiune în masă: Marchează în starea "pending" rolul pentru toți utilizatorii selectați
  bulkChangeRole(newRole: string): void {
    const role = newRole as UserRole;
    const ids = Array.from(this.selectedUserIds());

    if (ids.length === 0) return;

    this.pendingRoles.update((map) => {
      const next = new Map(map);
      ids.forEach((id) => {
        const user = this.users().find((u) => u.id === id);
        if (user) {
          if (role === user.role) {
            next.delete(id);
          } else {
            next.set(id, role);
          }
        }
      });
      return next;
    });

    this.selectedUserIds.set(new Set());
  }

  getDisplayRole(user: UserDto): UserRole {
    return this.pendingRoles().get(user.id) ?? user.role;
  }

  hasPendingChange(user: UserDto): boolean {
    return this.pendingRoles().has(user.id);
  }

  onRoleChange(user: UserDto, newRole: string): void {
    const role = newRole as UserRole;
    this.pendingRoles.update((map) => {
      const next = new Map(map);
      if (role === user.role) {
        next.delete(user.id);
      } else {
        next.set(user.id, role);
      }
      return next;
    });
  }

  saveChanges(): void {
    if (this.isSaving() || this.wouldLeaveZeroAdmins()) return;

    const entries = Array.from(this.pendingRoles().entries());
    const currentUserId = this.authService.currentUser()?.userId;
    const isSelfRoleChange = entries.some(([userId]) => userId === currentUserId);

    this.isSaving.set(true);
    this.errorMessage.set(null);

    from(entries)
      .pipe(
        concatMap(([userId, role]) =>
          this.usersService.updateRole(userId, { role }).pipe(
            tap((updated) => {
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
          this.pendingRoles.set(new Map());
          this.loadUsers();
          this.errorMessage.set(
            err?.error?.message ?? 'Nu am putut salva modificarile de rol.'
          );
        },
        complete: () => {
          this.isSaving.set(false);
          this.pendingRoles.set(new Map());

          if (isSelfRoleChange) {
            this.authService.logout();
            this.router.navigate(['/login'], { queryParams: { reason: 'role-changed' } });
          }
        }
      });
  }

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

  isCurrentUser(user: UserDto): boolean {
    return this.authService.currentUser()?.userId === user.id;
  }
}