/** Suggested account holder: "{FamilyName} {FatherName} ו{MotherName}" */
export function buildSuggestedAccountHolderName(
  familyLastName: string,
  fatherName: string,
  motherName: string,
): string {
  const family = familyLastName.trim();
  const father = fatherName.trim();
  const mother = motherName.trim();

  let result = family;
  if (father) {
    result = result ? `${result} ${father}` : father;
  }
  if (mother) {
    result = result ? `${result} ו${mother}` : mother;
  }
  return result;
}
