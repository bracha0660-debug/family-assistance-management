import type { OrgUserDto } from '../api/orgUsers';
import { translateRole } from './roleLabel';

interface UserCreatedConfirmationProps {
  user: OrgUserDto;
  onBackToList: () => void;
  onCreateAnother: () => void;
}

export function UserCreatedConfirmation({
  user,
  onBackToList,
  onCreateAnother,
}: UserCreatedConfirmationProps) {
  return (
    <section className="confirmation-panel" aria-labelledby="user-created-heading">
      <h2 id="user-created-heading">המשתמש נוצר בהצלחה</h2>
      <dl className="confirmation-details">
        <div>
          <dt>שם משתמש</dt>
          <dd><strong>{user.username}</strong></dd>
        </div>
        <div>
          <dt>שם מלא</dt>
          <dd><strong>{user.fullName}</strong></dd>
        </div>
        <div>
          <dt>תפקיד</dt>
          <dd><strong>{translateRole(user.role)}</strong></dd>
        </div>
      </dl>
      <div className="success" role="status" aria-live="polite">
        הסיסמה שהזנת לא תוצג שוב במערכת. ודאי שמסרת אותה למשתמש בערוץ מאובטח.
      </div>
      <div className="modal-actions">
        <button type="button" className="btn-secondary" onClick={onCreateAnother}>
          יצירת משתמש נוסף
        </button>
        <button type="button" onClick={onBackToList}>
          חזרה לרשימת המשתמשים
        </button>
      </div>
    </section>
  );
}
