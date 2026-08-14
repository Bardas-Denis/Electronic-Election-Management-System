import { Routes } from '@angular/router';
import {
  authGuard,
  adminGuard,
  electionManagerGuard,
  setupGuard
} from './core/guards/auth.guard';

// All pages lazy-loaded. authGuard = any logged-in user, adminGuard = Admin only, electionManagerGuard = Admin or ElectionManager.
// setupGuard wraps every app route and redirects to /setup when the backend is unconfigured.
export const routes: Routes = [
  // First-run setup wizard — open to anyone, intentionally outside the setupGuard wrapper.
  {
    path: 'setup',
    loadComponent: () =>
      import('./features/setup/setup.component').then((m) => m.SetupComponent)
  },

  // All other routes are wrapped in a parent that checks configuration status first.
  {
    path: '',
    canActivate: [setupGuard],
    children: [
      // Public marketing / front page, also reachable through the Votex brand.
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
      {
        path: 'admin/labels',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/admin/label-management.component').then(
            (m) => m.LabelManagementComponent
          )
      },

      // Any logged-in user — personal profile / details
      {
        path: 'profile',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/profile/profile.component').then(
            (m) => m.ProfileComponent
          )
      },

      { path: '**', redirectTo: '' }
    ]
  }
];
