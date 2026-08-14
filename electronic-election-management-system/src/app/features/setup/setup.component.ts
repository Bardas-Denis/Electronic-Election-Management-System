import { Component, inject, OnDestroy, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslatePipe } from '@ngx-translate/core';
import { interval, of, Subscription } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { SetupService } from '../../core/services/setup.service';
import { DbProvider } from '../../core/models/setup.model';

type ViewState = 'form' | 'restarting' | 'timeout';

const POLL_INTERVAL_MS = 2500;
const POLL_TIMEOUT_MS = 60_000;
const SQLITE_DEFAULT_CS = 'Data Source=election.db';

@Component({
  selector: 'app-setup',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './setup.component.html',
  styleUrl: './setup.component.scss'
})
export class SetupComponent implements OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly setupService = inject(SetupService);
  private readonly router = inject(Router);

  protected readonly viewState = signal<ViewState>('form');
  protected readonly selectedProvider = signal<DbProvider>('Sqlite');
  protected readonly isTesting = signal(false);
  protected readonly isSaving = signal(false);
  protected readonly showPassword = signal(false);

  // null = no test done yet | true = passed | false = failed
  protected readonly testPassed = signal<boolean | null>(null);
  protected readonly testErrorMessage = signal<string | null>(null);
  protected readonly serverErrorMessage = signal<string | null>(null);

  protected readonly pgForm = this.fb.nonNullable.group({
    host:     ['localhost', [Validators.required]],
    port:     ['5432',      [Validators.required, Validators.pattern(/^\d{1,5}$/)]],
    database: ['',          [Validators.required]],
    username: ['',          [Validators.required]],
    password: ['',          [Validators.required]]
  });

  private pollSub: Subscription | null = null;
  private timeoutId: ReturnType<typeof setTimeout> | null = null;

  // Provider selection

  protected selectProvider(p: DbProvider): void {
    this.selectedProvider.set(p);
    // Reset test state when the provider changes
    this.testPassed.set(null);
    this.testErrorMessage.set(null);
    this.serverErrorMessage.set(null);
  }

  // Test connection

  protected onTestConnection(): void {
    this.pgForm.markAllAsTouched();
    if (this.pgForm.invalid) return;

    this.isTesting.set(true);
    this.testPassed.set(null);
    this.testErrorMessage.set(null);
    this.serverErrorMessage.set(null);

    this.setupService
      .testConnection({ provider: 'Postgres', connectionString: this.buildPgConnectionString() })
      .subscribe({
        next: (res) => {
          this.isTesting.set(false);
          this.testPassed.set(res.success);
          if (!res.success) {
            this.testErrorMessage.set(res.error ?? null);
          }
        },
        error: () => {
          this.isTesting.set(false);
          this.testPassed.set(false);
          this.testErrorMessage.set(null); // generic message shown by template
        }
      });
  }

  // Save

  protected onSave(): void {
    const provider = this.selectedProvider();

    if (provider === 'Postgres') {
      this.pgForm.markAllAsTouched();
      if (this.pgForm.invalid) return;
    }

    this.isSaving.set(true);
    this.serverErrorMessage.set(null);

    const req =
      provider === 'Sqlite'
        ? { provider: 'Sqlite' as DbProvider, connectionString: SQLITE_DEFAULT_CS }
        : { provider: 'Postgres' as DbProvider, connectionString: this.buildPgConnectionString() };

    this.setupService.save(req).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.startPolling();
      },
      error: (err: HttpErrorResponse) => {
        this.isSaving.set(false);
        if (err.status === 409) {
          // Already configured — show brief message then go to login
          this.serverErrorMessage.set('alreadyConfigured');
          setTimeout(() => this.router.navigate(['/login']), 2000);
        } else {
          this.serverErrorMessage.set(err.error?.error ?? null);
        }
      }
    });
  }

  // Polling

  private startPolling(): void {
    this.viewState.set('restarting');

    this.timeoutId = setTimeout(() => {
      this.stopPolling();
      this.viewState.set('timeout');
    }, POLL_TIMEOUT_MS);

    this.pollSub = interval(POLL_INTERVAL_MS)
      .pipe(
        switchMap(() =>
          this.setupService.getStatus().pipe(
            catchError(() => of({ configured: false }))
          )
        )
      )
      .subscribe({
        next: ({ configured }) => {
          if (configured) {
            this.stopPolling();
            this.router.navigate(['/login']);
          }
        }
      });
  }

  private stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
    if (this.timeoutId !== null) {
      clearTimeout(this.timeoutId);
      this.timeoutId = null;
    }
  }

  // Helpers

  protected togglePassword(): void {
    this.showPassword.update((v) => !v);
  }

  protected reloadPage(): void {
    window.location.reload();
  }

  /** Assemble a Npgsql-compatible connection string from the form values. */
  private buildPgConnectionString(): string {
    const { host, port, database, username, password } = this.pgForm.getRawValue();
    return `Host=${host};Port=${port};Database=${database};Username=${username};Password=${password}`;
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }
}
