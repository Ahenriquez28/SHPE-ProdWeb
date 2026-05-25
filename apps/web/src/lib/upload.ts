/*
 * upload.ts — three-step file upload helper.
 *
 * Step 1: POST /api/files/presigned  → get a signed PUT URL from DO Spaces
 * Step 2: PUT <signedUrl>            → upload bytes directly from browser to Spaces
 * Step 3: POST /api/files            → register metadata (key, mime, size) in DB
 *
 * Why this pattern? The API never sees file bytes — no memory pressure.
 * The browser streams directly to Spaces, which handles large uploads gracefully.
 *
 * Throws ApiException on any API step failure; throws Error on PUT failure.
 */

import type { ApiError } from '@/types/api';
import { ApiException } from '@/lib/api';

export interface UploadResult {
  fileId: string;
  cdnUrl: string;
}

interface PresignedResponse {
  uploadUrl: string;
  spacesKey: string;
  cdnUrl: string;
  expiresAt: string;
}

interface FileResponse {
  id: string;
  cdnUrl: string;
  mimeType: string;
  sizeBytes: number;
  uploadedAt: string;
}

/**
 * uploadFile — uploads a File object and returns its registered metadata.
 *
 * @param file     the File selected by the user
 * @param folder   Spaces folder prefix, e.g. "headshots" or "flyers"
 * @param getToken Clerk's getToken function (from useAuth)
 */
export async function uploadFile(
  file: File,
  folder: string,
  getToken: () => Promise<string | null>,
): Promise<UploadResult> {
  const token = await getToken();
  const authHeader: Record<string, string> = token ? { Authorization: `Bearer ${token}` } : {};

  // Step 1 — get presigned upload URL
  const presignRes = await fetch('/api/files/presigned', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeader },
    body: JSON.stringify({
      fileName: file.name,
      mimeType: file.type,
      folder,
    }),
  });

  if (!presignRes.ok) {
    const problem = await presignRes.json().catch(() => ({
      type: 'unknown',
      title: presignRes.statusText,
      status: presignRes.status,
    })) as ApiError;
    throw new ApiException(presignRes.status, problem);
  }

  const { uploadUrl, spacesKey, cdnUrl } = (await presignRes.json()) as PresignedResponse;

  // Step 2 — PUT bytes directly to Spaces (no auth header — URL is pre-signed)
  const putRes = await fetch(uploadUrl, {
    method: 'PUT',
    headers: { 'Content-Type': file.type },
    body: file,
  });

  if (!putRes.ok) {
    throw new Error(`Spaces upload failed: ${putRes.status} ${putRes.statusText}`);
  }

  // Step 3 — register metadata in our DB
  const registerRes = await fetch('/api/files', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeader },
    body: JSON.stringify({
      spacesKey,
      cdnUrl,
      mimeType: file.type,
      sizeBytes: file.size,
    }),
  });

  if (!registerRes.ok) {
    const problem = await registerRes.json().catch(() => ({
      type: 'unknown',
      title: registerRes.statusText,
      status: registerRes.status,
    })) as ApiError;
    throw new ApiException(registerRes.status, problem);
  }

  const registered = (await registerRes.json()) as FileResponse;
  return { fileId: registered.id, cdnUrl: registered.cdnUrl };
}
