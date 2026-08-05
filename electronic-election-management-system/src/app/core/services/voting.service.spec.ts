import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { CastVoteRequest, CreateElectionRequest } from '../models/voting.model';
import { VotingService } from './voting.service';

describe('VotingService', () => {
  let service: VotingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        VotingService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(VotingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests elections visible to the current user', () => {
    service.getElections().subscribe(result => expect(result).toEqual([]));

    const request = http.expectOne(`${environment.apiUrl}/voting/elections`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('sends the complete election payload when creating an election', () => {
    const payload = electionRequest();
    service.createElection(payload).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/voting/elections`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);
    request.flush({ id: 'election-id', ...payload });
  });

  it('sends selected answers to the vote endpoint', () => {
    const payload: CastVoteRequest = {
      electionId: 'election-id',
      optionId: 'option-id',
      optionIds: ['option-id'],
      textAnswers: []
    };
    service.castVote(payload).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/voting/votes`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);
    request.flush(null);
  });

  it('uses both identifiers when removing an invitation', () => {
    service.removeElectionInvitation('election-id', 'invitation-id').subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/voting/elections/election-id/invitations/invitation-id`
    );
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('loads label audiences for a closed election', () => {
    service.getInvitationLabels().subscribe(labels => {
      expect(labels).toEqual([
        {
          id: 'label-id',
          name: 'Engineering',
          category: 'Department',
          userCount: 4
        }
      ]);
    });

    const request = http.expectOne(
      `${environment.apiUrl}/voting/elections/invitation-labels`
    );
    expect(request.request.method).toBe('GET');
    request.flush([
      {
        id: 'label-id',
        name: 'Engineering',
        category: 'Department',
        userCount: 4
      }
    ]);
  });
});

function electionRequest(): CreateElectionRequest {
  return {
    title: 'Board election',
    description: 'Choose a representative',
    question: 'Who should represent the board?',
    type: 'Comercial',
    isAnonymous: true,
    isClosed: false,
    isVisible: true,
    invitedUserIds: [],
    invitedEmails: [],
    startsAt: '2026-07-27T10:00:00Z',
    endsAt: '2026-07-27T11:00:00Z',
    options: [
      { label: 'Alice' },
      { label: 'Bob' }
    ],
    questions: []
  };
}
