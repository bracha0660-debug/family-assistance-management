export function isValidIsraeliId(id: string | null | undefined): boolean {
  if (!id || id.length !== 9) return false;

  let sum = 0;
  for (let i = 0; i < 9; i++) {
    const ch = id.charCodeAt(i);
    if (ch < 48 || ch > 57) return false;
    const digit = ch - 48;
    const weight = (i % 2) + 1;
    let product = digit * weight;
    if (product > 9) product -= 9;
    sum += product;
  }

  return sum % 10 === 0;
}
