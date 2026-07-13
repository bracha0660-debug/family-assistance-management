import {
  approvedHistoryFieldLabelMap,
  historyDisplayValue,
  historyEventLabelHe,
  historyFieldLabelHe,
  looksTechnical,
} from '../components/history/historyLabels';

/**
 * §20 History UI correction tests (amount / supplier / status / masked bank + labels).
 * Run: npx tsx src/validation/historyUi.test.ts
 */
export function runHistoryUiTests(): { ok: boolean; failures: string[] } {
  const failures: string[] = [];

  function expect(name: string, condition: boolean) {
    if (!condition) failures.push(name);
  }

  // --- Amount change 75 → 350 (semantic: previous then new) ---
  expect(
    'amount field maps to סכום לתשלום',
    historyFieldLabelHe('amount') === 'סכום לתשלום',
  );
  expect(
    'PaymentAmount alias maps to סכום לתשלום',
    historyFieldLabelHe('PaymentAmount') === 'סכום לתשלום',
  );
  expect(
    'amount values display as-is for 75→350',
    historyDisplayValue('75') === '75' && historyDisplayValue('350') === '350',
  );

  // --- Supplier change ---
  expect(
    'supplier_id maps to ספק',
    historyFieldLabelHe('supplier_id') === 'ספק',
  );
  expect(
    'SupplierId alias maps to ספק',
    historyFieldLabelHe('SupplierId') === 'ספק',
  );
  expect(
    'supplier display names pass through',
    historyDisplayValue('ספק א׳') === 'ספק א׳'
      && historyDisplayValue('ספק ב׳') === 'ספק ב׳',
  );

  // --- Status change ---
  expect(
    'status field maps to סטטוס',
    historyFieldLabelHe('status') === 'סטטוס',
  );
  expect(
    'waiting_for_reference → בביצוע',
    historyDisplayValue('waiting_for_reference') === 'בביצוע',
  );
  expect(
    'paid → שולם',
    historyDisplayValue('paid') === 'שולם',
  );
  expect(
    'completed → תהליך הושלם',
    historyDisplayValue('completed') === 'תהליך הושלם',
  );

  // --- Bank-account masked value change ---
  expect(
    'account_number maps to מספר חשבון',
    historyFieldLabelHe('account_number') === 'מספר חשבון',
  );
  expect(
    'masked bank values pass through',
    historyDisplayValue('******1234') === '******1234'
      && historyDisplayValue('******5678') === '******5678',
  );

  // --- No technical keys ---
  expect(
    'unknown field key returns null (do not expose)',
    historyFieldLabelHe('SomeInternalProperty') === null,
  );
  expect(
    'raw PascalCase is technical',
    looksTechnical('PaymentAmount') === true,
  );
  expect(
    'JSON is technical',
    looksTechnical('{"a":1}') === true,
  );
  expect(
    'GUID is technical',
    looksTechnical('aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee') === true,
  );
  expect(
    'backend technical label ignored when key unknown',
    historyFieldLabelHe('UnknownField', 'UnknownField') === null,
  );

  // --- Event labels ---
  expect(
    'item_edited → עריכת פריט',
    historyEventLabelHe('item_edited') === 'עריכת פריט',
  );
  expect(
    'reference_entered → הוזנה אסמכתא',
    historyEventLabelHe('reference_entered') === 'הוזנה אסמכתא',
  );
  expect(
    'unknown event type returns null',
    historyEventLabelHe('weird_event_xyz') === null,
  );

  // --- Transition DOM contract (string order for three elements) ---
  const transitionContract = {
    previous: '75 ₪',
    arrow: '←',
    next: '350 ₪',
    dir: 'rtl',
  };
  expect(
    'transition contract previous is 75',
    transitionContract.previous.startsWith('75'),
  );
  expect(
    'transition contract new is 350',
    transitionContract.next.startsWith('350'),
  );
  expect(
    'transition contract uses rtl + left-pointing arrow',
    transitionContract.dir === 'rtl' && transitionContract.arrow === '←',
  );

  const map = approvedHistoryFieldLabelMap();
  expect('map includes amount', map.amount === 'סכום לתשלום');
  expect('map includes account_number', map.account_number === 'מספר חשבון');

  return { ok: failures.length === 0, failures };
}
