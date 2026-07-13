import { runSuspendedRecoveryUiTests } from '../src/validation/suspendedRecoveryUi.test.ts';

const result = runSuspendedRecoveryUiTests();
if (!result.ok) {
  console.error('FAIL suspendedRecoveryUi.test.ts', result.failures);
  process.exit(1);
}
console.log('PASS suspendedRecoveryUi.test.ts all assertions ok');
