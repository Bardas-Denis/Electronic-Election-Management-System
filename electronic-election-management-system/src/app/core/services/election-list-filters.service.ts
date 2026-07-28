import { Injectable, signal } from '@angular/core';

export interface ElectionListFilters {
  political: boolean;
  commercial: boolean;
  anonymous: boolean;
  nonAnonymous: boolean;
  voted: boolean;
  unvoted: boolean;
  expired: boolean;
  active: boolean;
  thisWeek: boolean;
  moreThanAMonth: boolean;
}

const DEFAULT_FILTERS: ElectionListFilters = {
  political: false,
  commercial: false,
  anonymous: false,
  nonAnonymous: false,
  voted: false,
  unvoted: false,
  expired: false,
  active: false,
  thisWeek: false,
  moreThanAMonth: false
};

// Holds the election-list search/filter selection outside the component,
// so it survives navigating away (e.g. to vote/results) and back - a routed
// component is destroyed and recreated on each visit, this service isn't.
@Injectable({ providedIn: 'root' })
export class ElectionListFiltersService {
  selectedFilters = signal<ElectionListFilters>({ ...DEFAULT_FILTERS });
  searchQuery = signal<string>('');
}
