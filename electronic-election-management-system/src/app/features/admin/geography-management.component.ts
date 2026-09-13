import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { GeographyService } from '../../core/services/geography.service';
import { LabelService } from '../../core/services/label.service';
import { GEOGRAPHIC_KINDS, GeographicNode, isKnownKind } from '../../core/models/geography.model';
import { INPUT_LIMITS } from '../../core/validators/input.validators';
import { removeDiacritics } from '../../core/utils/text.util';

/**
 * Folds a value for comparison, so a search matches what people actually type: the data holds
 * "Timis" while a Romanian keyboard produces the accented spelling, and neither should hide
 * the other.
 *
 * The diacritic folding itself is shared with the profile, which uses it when storing a typed
 * city name. Case and surrounding space are dropped only here: storing a name must keep both.
 */
function normalize(value: string): string {
  return removeDiacritics(value).toLowerCase().trim();
}

/**
 * Browses and curates the geographic tree one level at a time: countries, then a country's
 * subdivisions, then the localities under those.
 *
 * Deliberately a breadcrumb and a list rather than an expandable tree — the United Kingdom
 * alone has 232 subdivisions, and a fully expanded tree would be unreadable and would pull
 * thousands of rows into one page.
 */
@Component({
  selector: 'app-geography-management',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './geography-management.component.html',
  styleUrl: './geography-management.component.scss'
})
export class GeographyManagementComponent implements OnInit {
  private geography = inject(GeographyService);
  private labels = inject(LabelService);
  private translate = inject(TranslateService);
  readonly INPUT_LIMITS = INPUT_LIMITS;
  readonly kinds = GEOGRAPHIC_KINDS;

  /** Root-to-current trail. Empty means we are looking at the list of countries. */
  path = signal<GeographicNode[]>([]);
  items = signal<GeographicNode[]>([]);

  isLoading = signal(true);
  isSaving = signal(false);
  errorKey = signal<string | null>(null);
  successKey = signal<string | null>(null);

  showCreateForm = signal(false);
  newName = signal('');
  newKind = signal('');

  editingId = signal<string | null>(null);
  editName = signal('');
  editKind = signal('');

  /** Which row is asking "are you sure?". Only ever one at a time. */
  confirmingDeleteId = signal<string | null>(null);

  searchTerm = signal('');

  /**
   * Which kind picker is open: 'new' for the add form, a node id for a row, null for none.
   * A native select cannot be styled once it opens — the list is drawn by the operating
   * system — so this one is built from a button and a panel like the app's other menus.
   */
  kindMenuFor = signal<string | null>(null);

  /** What has been typed into the open picker. Cleared every time one opens. */
  kindSearch = signal('');

  /**
   * Filtering happens here rather than on the server: one level is at most a few hundred rows
   * and is already loaded, so a round trip would only add latency to every keystroke.
   */
  visibleItems = computed(() => {
    const term = normalize(this.searchTerm());
    if (!term) return this.items();
    return this.items().filter(
      (n) => normalize(n.name).includes(term) || normalize(n.code ?? '').includes(term)
    );
  });

  /** True when a search is hiding some of the level. */
  isFiltered = computed(() => this.searchTerm().trim().length > 0);

  /** The node whose children are on screen, or null at the country level. */
  currentParent = computed(() => this.path().at(-1) ?? null);

  /**
   * Countries come from the seed and are not created by hand: there is no ISO list to pick
   * from here, and a hand-typed country would sit outside every code the tree relies on.
   */
  canAddHere = computed(() => this.currentParent() !== null);

  ngOnInit(): void {
    this.loadCountries();
  }

  private loadCountries(): void {
    this.isLoading.set(true);
    this.errorKey.set(null);
    this.geography.getCountries().subscribe({
      next: (data) => {
        this.items.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorKey.set('geography.loadFailed');
        this.isLoading.set(false);
      }
    });
  }

  private loadChildren(parentId: string): void {
    this.isLoading.set(true);
    this.errorKey.set(null);
    this.geography.getChildren(parentId).subscribe({
      next: (data) => {
        this.items.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorKey.set(this.errorKeyFrom(err, 'geography.loadFailed'));
        this.isLoading.set(false);
      }
    });
  }

  /** Reloads whatever level is currently on screen. */
  private reload(): void {
    const parent = this.currentParent();
    if (parent) this.loadChildren(parent.id);
    else this.loadCountries();
  }

  open(node: GeographicNode): void {
    this.cancelRename();
    this.cancelDelete();
    this.closeCreateForm();
    this.searchTerm.set('');
    this.path.update((p) => [...p, node]);
    this.loadChildren(node.id);
  }

  /** A breadcrumb index of -1 is the country level above every node. */
  goTo(index: number): void {
    this.cancelRename();
    this.cancelDelete();
    this.closeCreateForm();
    this.searchTerm.set('');
    this.path.update((p) => p.slice(0, index + 1));
    this.reload();
  }

  // ── Creating ─────────────────────────────────────────────────────────────

  openCreateForm(): void {
    this.showCreateForm.set(true);
    this.newName.set('');
    this.newKind.set('');
    this.errorKey.set(null);
    this.successKey.set(null);
  }

  closeCreateForm(): void {
    this.showCreateForm.set(false);
    this.newName.set('');
    this.newKind.set('');
  }

  create(): void {
    const parent = this.currentParent();
    const name = this.newName().trim();
    if (!parent || !name) return;

    this.isSaving.set(true);
    this.errorKey.set(null);

    this.geography.createChild({ parentId: parent.id, name, kind: this.newKind().trim() || null }).subscribe({
      next: (created) => {
        this.items.update((list) =>
          [...list, created].sort((a, b) => a.name.localeCompare(b.name))
        );
        this.isSaving.set(false);
        this.closeCreateForm();
        this.flashSuccess('geography.createSuccess');
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorKey.set(this.errorKeyFrom(err, 'geography.createFailed'));
      }
    });
  }

  // ── Renaming ─────────────────────────────────────────────────────────────

  startEdit(node: GeographicNode): void {
    this.closeCreateForm();
    this.cancelDelete();
    this.editingId.set(node.id);
    this.editName.set(node.name);
    this.editKind.set(node.kind ?? '');
    this.errorKey.set(null);
  }

  cancelRename(): void {
    this.editingId.set(null);
    this.editName.set('');
    this.editKind.set('');
  }

  saveEdit(node: GeographicNode): void {
    const name = this.editName().trim();
    if (!name) return;

    const kind = this.editKind().trim() || null;

    // Nothing changed, so skip the round trip rather than reporting a clash with itself.
    if (name === node.name && kind === (node.kind ?? null)) {
      this.cancelRename();
      return;
    }

    this.isSaving.set(true);
    this.errorKey.set(null);

    this.geography.update(node.id, { name, kind }).subscribe({
      next: (updated) => {
        this.items.update((list) =>
          list
            .map((n) => (n.id === updated.id ? updated : n))
            .sort((a, b) => a.name.localeCompare(b.name))
        );
        // The breadcrumb may hold this node too, if a child level is what we came from.
        this.path.update((p) => p.map((n) => (n.id === updated.id ? updated : n)));
        this.isSaving.set(false);
        this.cancelRename();
        this.flashSuccess('geography.renameSuccess');
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorKey.set(this.errorKeyFrom(err, 'geography.renameFailed'));
      }
    });
  }


  // ── Deleting ─────────────────────────────────────────────────────────────

  /**
   * Deletion goes through the labels endpoint rather than a geography-specific one: that is
   * the single path that moves a node's users up to its parent, and a second route would be
   * a way to bypass it.
   */
  /**
   * Asks in the row rather than through window.confirm. The native dialog is suppressed inside
   * embedded browser panes, where it returns false without ever appearing — the delete button
   * then looks broken because nothing at all happens.
   */
  askDelete(node: GeographicNode): void {
    this.cancelRename();
    this.closeCreateForm();
    this.errorKey.set(null);
    this.confirmingDeleteId.set(node.id);
  }

  cancelDelete(): void {
    this.confirmingDeleteId.set(null);
  }

  remove(node: GeographicNode): void {
    this.confirmingDeleteId.set(null);
    this.errorKey.set(null);
    this.labels.deleteLabel(node.id).subscribe({
      next: () => {
        this.items.update((list) => list.filter((n) => n.id !== node.id));
        this.flashSuccess('geography.deleteSuccess');
      },
      error: (err) => {
        this.errorKey.set(this.errorKeyFrom(err, 'geography.deleteFailed'));
      }
    });
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  openKindMenu(key: string, event: Event): void {
    // Without this the document listener below would see the same click and shut the panel
    // in the same tick it opened.
    event.stopPropagation();
    if (this.kindMenuFor() === key) return;
    this.kindSearch.set('');
    this.kindMenuFor.set(key);
  }

  onKindSearch(key: string, text: string): void {
    // Typing in a closed picker opens it, so the field behaves like one search box rather
    // than needing a click first.
    if (this.kindMenuFor() !== key) this.kindMenuFor.set(key);
    this.kindSearch.set(text);
  }

  pickKind(key: string, value: string): void {
    if (key === 'new') this.newKind.set(value);
    else this.editKind.set(value);
    this.kindMenuFor.set(null);
    this.kindSearch.set('');
  }

  /**
   * What the field shows: the typed filter while the picker is open, the chosen kind once it
   * closes. Without the swap, typing would appear to erase a selection that is still there.
   */
  kindFieldText(key: string, value: string): string {
    if (this.kindMenuFor() === key) return this.kindSearch();
    return value ? this.translate.instant('geography.kinds.' + value) : '';
  }

  /**
   * The kinds whose translated name matches what has been typed. Matching on the words the
   * user can actually see, not on the stored keys, and ignoring diacritics so "judet" finds
   * "Județ".
   */
  filteredKinds(): string[] {
    const term = normalize(this.kindSearch());
    if (!term) return [...this.kinds];
    return this.kinds.filter((k) =>
      normalize(this.translate.instant('geography.kinds.' + k)).includes(term)
    );
  }

  /** True when the "unspecified" row should be offered — it is not a kind, so it never filters. */
  showUnspecified(): boolean {
    return !this.kindSearch().trim();
  }

  @HostListener('document:click')
  closeKindMenu(): void {
    this.kindMenuFor.set(null);
    this.kindSearch.set('');
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeKindMenu();
  }

  /**
   * The translation key for a node's kind, or null when it carries something the picker does
   * not know — a value straight from the seed, which is shown as typed.
   */
  kindKey(node: GeographicNode): string | null {
    return isKnownKind(node.kind) ? 'geography.kinds.' + node.kind : null;
  }

  /** Prefers the server's error code, so the user sees why rather than a generic failure. */
  private errorKeyFrom(err: unknown, fallback: string): string {
    const code = (err as { error?: { errorCode?: string } })?.error?.errorCode;
    return code ? `errors.${code}` : fallback;
  }

  private flashSuccess(key: string): void {
    this.successKey.set(key);
    setTimeout(() => this.successKey.set(null), 3000);
  }
}
