import { Routes } from '@angular/router';
import { authGuard, adminGuard, electionManagerGuard } from './core/guards/auth.guard';

// All pages lazy-loaded. authGuard = any logged-in user, adminGuard = Admin only, electionManagerGuard = Admin or ElectionManager.
export const routes: Routes = [
  // Public marketing / front page. Redirects logged-in users to /elections itself.
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/home/home.component').then((m) => m.HomeComponent)
  },

  // Public auth pages
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

  // Any logged-in user
  {
    path: 'elections',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/voting/election-list.component').then(
        (m) => m.ElectionListComponent
      )
  },

  // Admin or ElectionManager
  {
    path: 'elections/mine',
    canActivate: [electionManagerGuard],
    loadComponent: () =>
      import('./features/voting/my-elections.component').then(
        (m) => m.MyElectionsComponent
      )
  },
  {
    path: 'elections/new',
    canActivate: [electionManagerGuard],
    loadComponent: () =>
      import('./features/voting/create-election.component').then(
        (m) => m.CreateElectionComponent
      )
  },
  // Same component as create - shared for both new/edit
  {
    path: 'elections/:id/edit',
    canActivate: [electionManagerGuard],
    loadComponent: () =>
      import('./features/voting/create-election.component').then(
        (m) => m.CreateElectionComponent
      )
  },

  // Any logged-in user
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

  // Admin only
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