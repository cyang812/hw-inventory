// Typed fetch wrapper. Maps the API's ListResponse + ErrorResponse shapes onto
// browser-friendly promise rejections so calling code can rely on
// `try { await api.get(...) }` and catch with `(err as ApiError)`.

const TOKEN_KEY = 'hwInventory.authToken';

export interface ApiError extends Error {
  status: number;
  error: string;
  detail?: string;
}

export class HttpError extends Error implements ApiError {
  status: number;
  error: string;
  detail?: string;
  constructor(status: number, error: string, detail?: string) {
    super(`${error}: ${detail ?? ''}`);
    this.status = status;
    this.error = error;
    this.detail = detail;
  }
}

function buildHeaders(custom?: HeadersInit, isJson = false): HeadersInit {
  const headers: Record<string, string> = {};
  if (isJson) headers['Content-Type'] = 'application/json';
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (custom) {
    if (custom instanceof Headers) {
      custom.forEach((v, k) => { headers[k] = v; });
    } else if (Array.isArray(custom)) {
      for (const [k, v] of custom) headers[k] = v;
    } else {
      Object.assign(headers, custom);
    }
  }
  return headers;
}

async function handle<T>(resp: Response): Promise<T> {
  if (resp.ok) {
    if (resp.status === 204) return undefined as unknown as T;
    const ct = resp.headers.get('content-type') ?? '';
    if (ct.includes('application/json')) return (await resp.json()) as T;
    return (await resp.text()) as unknown as T;
  }
  let error = 'http_error';
  let detail: string | undefined;
  try {
    const body = await resp.json();
    error = body?.error ?? error;
    detail = body?.detail;
  } catch {
    /* non-json body */
  }
  const err = new HttpError(resp.status, error, detail);
  console.error(`[api] ${resp.status} ${resp.url} → ${error}${detail ? ': ' + detail : ''}`);
  throw err;
}

export const api = {
  setToken(token: string | null) {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  },
  hasToken(): boolean {
    return !!localStorage.getItem(TOKEN_KEY);
  },
  async get<T>(path: string, params?: Record<string, unknown>): Promise<T> {
    const url = buildUrl(path, params);
    return handle<T>(await fetch(url, { headers: buildHeaders() }));
  },
  async post<T>(path: string, body?: unknown): Promise<T> {
    return handle<T>(await fetch(path, {
      method: 'POST',
      headers: buildHeaders(undefined, body !== undefined),
      body: body === undefined ? undefined : JSON.stringify(body),
    }));
  },
  async put<T>(path: string, body?: unknown): Promise<T> {
    return handle<T>(await fetch(path, {
      method: 'PUT',
      headers: buildHeaders(undefined, body !== undefined),
      body: body === undefined ? undefined : JSON.stringify(body),
    }));
  },
  async patch<T>(path: string, ops: Array<{ op: string; path: string; value?: unknown }>): Promise<T> {
    return handle<T>(await fetch(path, {
      method: 'PATCH',
      headers: buildHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(ops),
    }));
  },
  async del<T = void>(path: string, params?: Record<string, unknown>): Promise<T> {
    return handle<T>(await fetch(buildUrl(path, params), { method: 'DELETE', headers: buildHeaders() }));
  },
};

function buildUrl(path: string, params?: Record<string, unknown>): string {
  if (!params) return path;
  const usp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v == null) continue;
    if (Array.isArray(v)) v.forEach((item) => usp.append(k, String(item)));
    else usp.append(k, String(v));
  }
  const q = usp.toString();
  return q ? `${path}${path.includes('?') ? '&' : '?'}${q}` : path;
}
