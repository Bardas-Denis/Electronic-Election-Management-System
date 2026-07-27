import { describe, expect, it } from 'vitest';
import { ElectionDto } from '../../core/models/voting.model';
import { normalizeEditableQuestions } from './create-election.component';

describe('normalizeEditableQuestions', () => {
  it('preserves current question and option values for editing', () => {
    const election: ElectionDto = {
      ...baseElection(),
      question: 'Who should lead?',
      questions: [{
        id: 'question-id',
        text: 'Who should lead?',
        displayOrder: 0,
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
});

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
