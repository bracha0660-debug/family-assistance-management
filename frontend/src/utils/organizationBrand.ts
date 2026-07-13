import type { UserDto } from '../api/auth';

export function resolveOrganizationBrandLogo(
  user: Pick<UserDto, 'organizationLogoUrl' | 'organizationName'>,
): { logoSrc?: string; logoAlt: string } {
  const trimmed = user.organizationLogoUrl?.trim();
  return {
    logoSrc: trimmed || undefined,
    logoAlt: user.organizationName ? `לוגו ${user.organizationName}` : 'לוגו הארגון',
  };
}
