import { describe, expect, it } from 'vitest';
import { ElectionDto, ElectionInvitationDto, InvitationLabelDto } from '../../core/models/voting.model';
import {
  computeInvitationDiff,
  expandAudienceGroups,
  freeTextTypeSideEffects,
  normalizeEditableQuestions
} from './create-election.component';

// normalizeEditableQuestions

describe('normalizeEditableQuestions', () => {
  it('preserves current question and option values for editing', () => {
    const election: ElectionDto = {
      ...baseElection(),
      question: 'Who should lead?',
      questions: [{
        id: 'question-id',
        text: 'Who should lead?',
        displayOrder: 0,
        isRequired: true,
        allowMultipleAnswers: false,
        questionType: 'Choice',
        allowOtherOption: false,
        requiredRankCount: null,
        options: [
          { id: 'one', label: 'Alice', description: 'Candidate one' },
          { id: 'two', label: 'Bob', description: 'Candidate two' }
        ]
      }]
    };

    const result = normalizeEditableQuestions(election);

    expect(result[0].text).toBe(election.questions[0].text);
    expect(result[0].options.map(option => option.label))
      .toEqual(election.questions[0].options.map(option => option.label));
  });

  it('loads legacy single-question elections from top-level options', () => {
    const election: ElectionDto = {
      ...baseElection(),
      question: 'Choose a location',
      options: [
        { id: 'one', label: 'Mountains' },
        { id: 'two', label: 'Sea' }
      ]
    };

    const result = normalizeEditableQuestions(election);

    expect(result[0].text).toBe(election.question);
    expect(result[0].options.map(option => option.label))
      .toEqual(election.options.map(option => option.label));
  });

  it('carries image ids back into the edit form', () => {
    const election: ElectionDto = {
      ...baseElection(),
      question: 'Who should lead?',
      questions: [{
        id: 'question-id',
        text: 'Who should lead?',
        displayOrder: 0,
        isRequired: true,
        allowMultipleAnswers: false,
        questionType: 'Choice',
        allowOtherOption: false,
        requiredRankCount: null,
        imageId: 'question-image',
        options: [
          { id: 'one', label: 'Alice', imageId: 'option-image' },
          { id: 'two', label: 'Bob' }
        ]
      }]
    };

    const result = normalizeEditableQuestions(election);

    expect(result[0].imageId).toBe('question-image');
    expect(result[0].options[0].imageId).toBe('option-image');
    // Absent rather than empty string: the API binds Guid?, which rejects "".
    expect(result[0].options[1].imageId).toBeNull();
  });

  it('defaults image ids to null when the election has none', () => {
    const result = normalizeEditableQuestions({
      ...baseElection(),
      question: 'Choose a location',
      options: [{ id: 'one', label: 'Mountains' }, { id: 'two', label: 'Sea' }]
    });

    expect(result[0].imageId).toBeNull();
    expect(result[0].options.every(option => option.imageId === null)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// freeTextTypeSideEffects
// ---------------------------------------------------------------------------

describe('freeTextTypeSideEffects', () => {
  it('clears allowMultipleAnswers and allowOtherOption when switching to FreeText', () => {
    expect(freeTextTypeSideEffects('FreeText')).toEqual({
      allowMultipleAnswers: false,
      allowOtherOption: false
    });
  });

  it('returns null for Choice, since those fields stay under the admin\'s control', () => {
    expect(freeTextTypeSideEffects('Choice')).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// computeInvitationDiff
// ---------------------------------------------------------------------------

describe('computeInvitationDiff', () => {
  it('returns empty diff when nothing has changed', () => {
    const existing = [
      userInvitation('user-1', 'alice@example.com', 'inv-1'),
      emailInvitation('bob@example.com', 'inv-2')
    ];

    const { toAdd, toRemove } = computeInvitationDiff(
      existing,
      ['user-1'],
      ['bob@example.com']
    );

    expect(toAdd.userIds).toHaveLength(0);
    expect(toAdd.emails).toHaveLength(0);
    expect(toRemove).toHaveLength(0);
  });

  it('puts new user IDs into toAdd when they are not in existing', () => {
    const { toAdd } = computeInvitationDiff([], ['user-new-1', 'user-new-2'], []);

    expect(toAdd.userIds).toContain('user-new-1');
    expect(toAdd.userIds).toContain('user-new-2');
    expect(toAdd.emails).toHaveLength(0);
  });

  it('puts new emails into toAdd when they are not in existing', () => {
    const { toAdd } = computeInvitationDiff([], [], ['new@example.com']);

    expect(toAdd.emails).toContain('new@example.com');
    expect(toAdd.userIds).toHaveLength(0);
  });

  it('puts the invitation ID into toRemove when a user is no longer desired', () => {
    const existing = [userInvitation('user-1', 'alice@example.com', 'inv-1')];

    const { toRemove } = computeInvitationDiff(existing, [], []);

    expect(toRemove).toContain('inv-1');
  });

  it('puts the invitation ID into toRemove when an email is no longer desired', () => {
    const existing = [emailInvitation('old@example.com', 'inv-2')];

    const { toRemove } = computeInvitationDiff(existing, [], []);

    expect(toRemove).toContain('inv-2');
  });

  it('matches email invitations case-insensitively so no spurious add/remove', () => {
    const existing = [emailInvitation('voter@example.com', 'inv-1')];

    // Same address but uppercase — should be treated as unchanged
    const { toAdd, toRemove } = computeInvitationDiff(existing, [], ['VOTER@EXAMPLE.COM']);

    expect(toRemove).toHaveLength(0);
    expect(toAdd.emails).toHaveLength(0);
  });

  it('correctly handles a mixed add-and-remove in one call', () => {
    const existing = [
      userInvitation('user-keep', 'keep@example.com', 'inv-keep'),
      emailInvitation('remove@example.com', 'inv-remove')
    ];

    const { toAdd, toRemove } = computeInvitationDiff(
      existing,
      ['user-keep', 'user-new'],  // user-keep stays, user-new is added
      []                           // remove@example.com is dropped
    );

    expect(toAdd.userIds).toEqual(['user-new']);
    expect(toAdd.emails).toHaveLength(0);
    expect(toRemove).toContain('inv-remove');
    expect(toRemove).not.toContain('inv-keep');
  });
});

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function baseElection(): ElectionDto {
  return {
    id: 'election-id',
    title: 'Election title',
    type: 'Comercial',
    isAnonymous: true,
    isClosed: false,
    startsAt: '2026-07-27T10:00:00Z',
    endsAt: '2026-07-28T10:00:00Z',
    options: [],
    questions: []
  };
}

function userInvitation(userId: string, email: string, id: string): ElectionInvitationDto {
  return { id, userId, email, method: 'Manual', createdAt: '' };
}

function emailInvitation(email: string, id: string): ElectionInvitationDto {
  return { id, userId: undefined, email, method: 'Email', createdAt: '' };
}

// expandAudienceGroups (pure frontend AND/OR/NOT evaluator)


describe('expandAudienceGroups', () => {
  it('returns empty set when groups are empty', () => {
    const result = expandAudienceGroups([], []);
    expect(result).toHaveLength(0);
  });

  it('includes users who have all positive labels in a single AND-group', () => {
    const clujId = 'label-cluj';
    const hrId = 'label-hr';
    const userBoth = 'user-both';
    const userOneOnly = 'user-one';
    const labels = [
      makeLabel(clujId, [userBoth, userOneOnly]),
      makeLabel(hrId, [userBoth])
    ];
    
    const groups = [{ conditions: [{ labelId: clujId, isExcluded: false }, { labelId: hrId, isExcluded: false }] }];

    const result = expandAudienceGroups(groups, labels);

    expect(result).toContain(userBoth);
    expect(result).not.toContain(userOneOnly);
  });

  it('unions two OR-groups and deduplicates users appearing in both', () => {
    const clujId = 'label-cluj';
    const bucId = 'label-buc';
    const userCluj = 'user-cluj';
    const userBuc = 'user-buc';
    const userBoth = 'user-both';
    const labels = [
      makeLabel(clujId, [userCluj, userBoth]),
      makeLabel(bucId, [userBuc, userBoth])
    ];
    // Cluj OR Bucharest
    const groups = [
      { conditions: [{ labelId: clujId, isExcluded: false }] },
      { conditions: [{ labelId: bucId, isExcluded: false }] }
    ];

    const result = expandAudienceGroups(groups, labels);

    expect(result).toContain(userCluj);
    expect(result).toContain(userBuc);
    expect(result).toContain(userBoth);
    // No duplicates
    expect(new Set(result).size).toBe(result.length);
  });

  it('excludes users who have the NOT-condition label', () => {
    const hrId = 'label-hr';
    const youngId = 'label-young';
    const adultUser = 'user-adult';
    const youngUser = 'user-young';
    const labels = [
      makeLabel(hrId, [adultUser, youngUser]),
      makeLabel(youngId, [youngUser])
    ];
    // HR AND NOT young
    const groups = [{ conditions: [{ labelId: hrId, isExcluded: false }, { labelId: youngId, isExcluded: true }] }];

    const result = expandAudienceGroups(groups, labels);

    expect(result).toContain(adultUser);
    expect(result).not.toContain(youngUser);
  });

  it('skips a group where all conditions are excluded (no positive conditions)', () => {
    const labels = [makeLabel('label-x', ['user-x'])];
    // A group with only a NOT condition — should produce zero members
    const groups = [{ conditions: [{ labelId: 'label-x', isExcluded: true }] }];

    const result = expandAudienceGroups(groups, labels);

    expect(result).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeLabel(id: string, userIds: string[]): InvitationLabelDto {
  return { id, name: id, userCount: userIds.length, userIds };
}
