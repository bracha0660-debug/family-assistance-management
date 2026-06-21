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
    case 'OrganizationUser':
      return 'משתמש ארגון';
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
    case 'permissions_update':
      return 'עדכון הרשאות תפקיד';
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
    case 'family_last_name':
      return 'שם משפחה';
    case 'external_accounting_number':
      return 'מספר חשבונאי';
    case 'father_name':
      return 'שם האב';
    case 'father_israeli_id':
      return 'ת.ז. האב';
    case 'mother_name':
      return 'שם האם';
    case 'mother_israeli_id':
      return 'ת.ז. האם';
    case 'number_of_children':
      return 'מספר ילדים';
    case 'head_of_household_name':
      return 'שם משפחה (ישן)';
    case 'head_id_number':
      return 'ת.ז. (ישן)';
    case 'household_size':
      return 'מספר ילדים (ישן)';
    case 'phone':
      return 'טלפון';
    case 'address':
      return 'כתובת';
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
