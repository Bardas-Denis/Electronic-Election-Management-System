import { describe, expect, it } from 'vitest';
import { ElectionDto } from '../../core/models/voting.model';
import { normalizeEditableQuestions } from './create-election.component';

const baseElection: ElectionDto = {
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

describe('normalizeEditableQuestions', () => {
  it('preserves current question and option values for editing', () => {
    const result = normalizeEditableQuestions({
      ...baseElection,
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
    });

    expect(result).toEqual([{
      text: 'Who should lead?',
      options: [
        { label: 'Alice', description: 'Candidate one', imageDataUrl: '' },
        { label: 'Bob', description: 'Candidate two', imageDataUrl: '' }
      ]
    }]);
  });

  it('loads legacy single-question elections from top-level options', () => {
    const result = normalizeEditableQuestions({
      ...baseElection,
      question: 'Choose a location',
      options: [
        { id: 'one', label: 'Mountains' },
        { id: 'two', label: 'Sea' }
      ]
    });

    expect(result[0].text).toBe('Choose a location');
    expect(result[0].options.map(option => option.label)).toEqual(['Mountains', 'Sea']);
  });
});
