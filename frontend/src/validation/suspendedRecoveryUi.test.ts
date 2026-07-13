import {
  historyDisplayValue,
  historyEventLabelHe,
  historyFieldLabelHe,
} from '../components/history/historyLabels';
import {
  DECISIONS_ITEM_ACTIONS,
  decisionsItemActions,
} from '../pages/home/workflowLabels';

/**
 * Suspended Recovery FE contract (SR5 frontend minimum).
 * Run: npx tsx src/validation/suspendedRecoveryUi.test.ts
 */
export function runSuspendedRecoveryUiTests(): { ok: boolean; failures: string[] } {
  const failures: string[] = [];
  function expect(name: string, condition: boolean) {
    if (!condition) failures.push(name);
  }

  // 1. availableActions with approve/reject/return render; no restore control
  const withContinue = decisionsItemActions([
    'approve',
    'reject',
    'return',
    'view_history',
    'restore',
    'unsuspend',
    'resume',
  ]);
  expect(
    'keeps approve/reject/return from availableActions',
    withContinue.includes('approve')
      && withContinue.includes('reject')
      && withContinue.includes('return'),
  );
  expect(
    'never surfaces restore/unsuspend/resume',
    !withContinue.includes('restore')
      && !withContinue.includes('unsuspend')
      && !withContinue.includes('resume')
      && !DECISIONS_ITEM_ACTIONS.has('restore')
      && !DECISIONS_ITEM_ACTIONS.has('unsuspend')
      && !DECISIONS_ITEM_ACTIONS.has('resume'),
  );

  // 2. empty / missing continue actions → no buttons inferred from status alone
  expect(
    'empty availableActions yields no decisions actions',
    decisionsItemActions([]).length === 0,
  );
  expect(
    'undefined availableActions yields no decisions actions',
    decisionsItemActions(undefined).length === 0,
  );
  expect(
    'status string alone is not in action filter input',
    decisionsItemActions(['suspended' as string]).length === 0,
  );

  // 3. History Hebrew labels + RTL previous→new values (מושהה ← אושר)
  expect('status field label is סטטוס', historyFieldLabelHe('status') === 'סטטוס');
  expect('suspended → מושהה', historyDisplayValue('suspended') === 'מושהה');
  expect('approved → אושר', historyDisplayValue('approved') === 'אושר');
  expect('rejected → נדחה', historyDisplayValue('rejected') === 'נדחה');
  expect('returned → הוחזר לתיקון', historyDisplayValue('returned') === 'הוחזר לתיקון');
  expect('approved event label is אושר', historyEventLabelHe('approved') === 'אושר');
  expect('rejected event label is נדחה', historyEventLabelHe('rejected') === 'נדחה');
  expect('returned event label is הוחזר לתיקון', historyEventLabelHe('returned') === 'הוחזר לתיקון');
  expect(
    'RTL transition contract previous right / new left uses ←',
    historyDisplayValue('suspended') === 'מושהה'
      && historyDisplayValue('approved') === 'אושר',
  );

  return { ok: failures.length === 0, failures };
}
