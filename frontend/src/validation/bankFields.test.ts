import {
  isBankCompleteForPayment,
  validateBankFieldsForPayment,
  type BankFields,
} from './bankFields';

const COMPLETE: BankFields = {
  bankNumber: '12',
  branchNumber: '123',
  accountNumber: '456789',
  accountHolderName: 'ישראל ישראלי',
};

export function runBankFieldsPaymentTests(): { ok: boolean; failures: string[] } {
  const failures: string[] = [];

  function expect(name: string, condition: boolean) {
    if (!condition) failures.push(name);
  }

  expect(
    'complete 4-field bank without bankName → isBankCompleteForPayment true',
    isBankCompleteForPayment(COMPLETE),
  );

  expect(
    'complete 4-field bank → validateBankFieldsForPayment null',
    validateBankFieldsForPayment('12', '123', '456789', 'ישראל ישראלי') === null,
  );

  expect(
    'all empty → isBankCompleteForPayment false',
    !isBankCompleteForPayment({
      bankNumber: '',
      branchNumber: '',
      accountNumber: '',
      accountHolderName: '',
    }),
  );

  expect(
    'partial fields → isBankCompleteForPayment false',
    !isBankCompleteForPayment({
      bankNumber: '12',
      branchNumber: '123',
      accountNumber: '',
      accountHolderName: 'ישראל ישראלי',
    }),
  );

  expect(
    'unknown bank number 99 → isBankCompleteForPayment false',
    !isBankCompleteForPayment({
      bankNumber: '99',
      branchNumber: '123',
      accountNumber: '456789',
      accountHolderName: 'ישראל ישראלי',
    }),
  );

  expect(
    'non-digit branch → isBankCompleteForPayment false',
    !isBankCompleteForPayment({
      bankNumber: '12',
      branchNumber: '12a',
      accountNumber: '456789',
      accountHolderName: 'ישראל ישראלי',
    }),
  );

  expect(
    'empty holder with filled digits → isBankCompleteForPayment false',
    !isBankCompleteForPayment({
      bankNumber: '12',
      branchNumber: '123',
      accountNumber: '456789',
      accountHolderName: '',
    }),
  );

  return { ok: failures.length === 0, failures };
}
