// AUTH STUBBED — Clerk useAuth removed. Requests go out without Bearer token.
import type { ApiError } from '@/types/api';

// ApiException is thrown on any non-2xx response.
// Callers can catch it and inspect .status or .problem.detail for user-facing messages.
export class ApiException extends Error {
  status: number;
  problem: ApiError;

  constructor(status: number, problem: ApiError) {
    super(problem.title || `HTTP ${status}`);
    this.status = status;
    this.problem = problem;
  }
}

// VITE_API_URL is empty in dev (Vite proxies /api → localhost:5001).
// In staging/prod it's set to https://staging.shpegsu.com/api etc.
const BASE_URL = import.meta.env.VITE_API_URL || '';

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  getToken?: () => Promise<string | null>,
): Promise<T> {
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  if (getToken) {
    const token = await getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    const problem = await res.json().catch(() => ({
      type: 'unknown',
      title: res.statusText,
      status: res.status,
    })) as ApiError;
    throw new ApiException(res.status, problem);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

import { useAuth } from '@clerk/react';

// useApi is a React hook — it reads getToken from Clerk on every render so the
// returned methods always have a fresh token closure.  Call it at the top of any
// component or custom hook that needs to hit the API.
export function useApi() {
  const { getToken } = useAuth();

  return {
    get:    <T,>(path: string)                 => request<T>('GET',    path, undefined, getToken),
    post:   <T,>(path: string, body?: unknown) => request<T>('POST',   path, body,      getToken),
    patch:  <T,>(path: string, body?: unknown) => request<T>('PATCH',  path, body,      getToken),
    delete: <T,>(path: string)                 => request<T>('DELETE', path, undefined, getToken),
  };
}
