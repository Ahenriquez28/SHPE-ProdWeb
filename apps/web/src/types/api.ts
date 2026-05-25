// Shared TypeScript types mirroring the C# DTOs.
//
// WHY HAND-WRITTEN (not codegen):
//   Simpler to debug in early weeks. Switch to OpenAPI codegen in Phase 2 if drift hurts.
//   RULE: when you add a field to a C# DTO, update the matching type here.

export type Uuid = string;
export type IsoDate = string;

// ─── Roles ─────────────────────────────────────────────────────────────────
export type Role =
  | 'member'
  | 'admin'
  | 'super_admin'
  | 'eboard'
  | 'alumni'
  | 'panelist'
  | 'sponsor_contact'
  | 'dev_team';

// ─── Person ────────────────────────────────────────────────────────────────
export type Person = {
  id: Uuid;
  fullName: string;
  email: string | null;
  gsuEmail: string | null;
  phone: string | null;
  gradYear: string | null;  // free-text like "May 2027", NOT a year integer
  linkedinUrl: string | null;
  roles: Role[];
  shareContact: boolean;
  smsOptIn: boolean;
  photoOptOut: boolean;
  createdAt: IsoDate;
};

// ─── Events ────────────────────────────────────────────────────────────────
export type EventPublic = {
  id: Uuid;
  title: string;
  description: string | null;
  flyerUrl: string | null;
  startAt: IsoDate;
  endAt: IsoDate | null;
  location: string | null;
  isPublic: true;
};

export type EventAdmin = EventPublic & {
  budgetCents: number | null;
  goalText: string | null;
  goalMet: boolean | null;
  attendeeCount: number;
  todoCount: number;
  todoCompleted: number;
};

// ─── E-Board ───────────────────────────────────────────────────────────────
export type EBoardMember = {
  id: Uuid;
  personId: Uuid;
  fullName: string;
  linkedInUrl: string | null;
  groupName: 'eboard' | 'dev_team';
  title: string;
  headshotUrl: string | null;
  sortOrder: number;
};

// ─── Sponsors ──────────────────────────────────────────────────────────────
export type Sponsor = {
  id: Uuid;
  name: string;
  logoUrl: string | null;
  websiteUrl: string | null;
  tier: string | null;
  sortOrder: number;
};

export type SponsorsResponse = {
  sponsors: Sponsor[];
  currentPacketUrl: string | null;
};

// ─── RFC 7807 error shape ──────────────────────────────────────────────────
// All API errors return this shape from ProblemDetailsResults.
export type ApiError = {
  type: string;
  title: string;
  status: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;  // validation errors keyed by field name
};
