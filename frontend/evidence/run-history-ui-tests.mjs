import { runHistoryUiTests } from '../src/validation/historyUi.test.ts';

const result = runHistoryUiTests();
if (!result.ok) {
  console.error('FAIL', result.failures);
  process.exit(1);
}
console.log('PASS historyUi.test.ts all assertions ok');
