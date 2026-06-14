export function translateRole(role: string): string {
  switch (role) {
    case 'SuperAdmin':
      return 'מנהל/ת מערכת';
    case 'OrganizationAdministrator':
      return 'מנהל/ת ארגון';
    case 'Coordinator':
      return 'מתאם/ת';
    case 'Manager':
      return 'מנהל/ת ועדה';
    case 'Finance':
      return 'כספים';
    default:
      return role;
  }
}

export function translateStatus(status: string): string {
  switch (status) {
    case 'active':
      return 'פעיל';
    case 'disabled':
      return 'מושבת';
    case 'suspended':
      return 'מושעה';
    default:
      return status;
  }
}

export function translateAction(action: string): string {
  switch (action) {
    case 'create':
      return 'יצירה';
    case 'update':
      return 'עדכון';
    case 'user_disable':
      return 'השבתת משתמש';
    case 'status_change':
      return 'שינוי סטטוס';
    default:
      return action;
  }
}

export function translateFieldName(field: string | null | undefined): string {
  if (!field) return '';
  switch (field) {
    case 'full_name':
      return 'שם מלא';
    case 'role':
      return 'תפקיד';
    case 'status':
      return 'סטטוס';
    default:
      return field;
  }
}
