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
    case 'inactive':
      return 'לא פעיל';
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
    case 'family_deactivate':
      return 'השבתת משפחה';
    case 'assistance_type_deactivate':
      return 'השבתת סוג סיוע';
    case 'organization_suspend':
      return 'השעיית ארגון';
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
    case 'head_of_household_name':
      return 'שם ראש משק בית';
    case 'head_id_number':
      return 'תעודת זהות';
    case 'phone':
      return 'טלפון';
    case 'address':
      return 'כתובת';
    case 'household_size':
      return 'גודל משק בית';
    case 'notes':
      return 'הערות';
    case 'name':
      return 'שם';
    case 'description':
      return 'תיאור';
    case 'default_amount':
      return 'סכום ברירת מחדל';
    case 'frequency':
      return 'תדירות';
    default:
      return field;
  }
}

export function translateFrequency(frequency: string): string {
  switch (frequency) {
    case 'one_time':
      return 'חד-פעמי';
    case 'monthly':
      return 'חודשי';
    case 'quarterly':
      return 'רבעוני';
    case 'annual':
      return 'שנתי';
    default:
      return frequency;
  }
}
