import { Routes } from '@angular/router';
import { authGuard, adminGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'elections', pathMatch: 'full' },

  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register.component').then((m) => m.RegisterComponent)
  },

  {
    path: 'elections',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/voting/election-list.component').then(
        (m) => m.ElectionListComponent
      )
  },
  {
    path: 'elections/new',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/voting/create-election.component').then(
        (m) => m.CreateElectionComponent
      )
  },
  {
    path: 'elections/:id/edit',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/voting/create-election.component').then(
        (m) => m.CreateElectionComponent
      )
  },
  {
    path: 'elections/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/voting/cast-vote.component').then(
        (m) => m.CastVoteComponent
      )
  },
  {
    path: 'elections/:id/results',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/dashboard/results-dashboard.component').then(
        (m) => m.ResultsDashboardComponent
      )
  },

  {
    path: 'admin/users',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/users-management.component').then(
        (m) => m.UsersManagementComponent
      )
  },

  { path: '**', redirectTo: 'elections' }
];
