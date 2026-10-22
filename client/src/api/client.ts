const BASE = "/api";

export class ApiError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${BASE}${path}`, {
      ...init,
      headers: {
        ...(init?.body ? { "Content-Type": "application/json" } : {}),
        ...(init?.headers ?? {})
      }
    });
  } catch {
    throw new ApiError("Cannot reach the local server. Is it running?", 0);
  }

  if (!response.ok) {
    let message = `Request failed (${response.status})`;
    try {
      const body = await response.json();
      if (body?.message) message = body.message;
    } catch {
      // Keep the status-based message.
    }
    throw new ApiError(message, response.status);
  }

  if (response.status === 204) return undefined as T;
  const text = await response.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

export const api = {
  get<T>(path: string): Promise<T> {
    return request<T>(path);
  },
  post<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });
  },
  patch<T>(path: string, body: unknown): Promise<T> {
    return request<T>(path, { method: "PATCH", body: JSON.stringify(body) });
  },
  del<T>(path: string): Promise<T> {
    return request<T>(path, { method: "DELETE" });
  },
  async upload<T>(path: string, file: File): Promise<T> {
    const response = await fetch(`${BASE}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: await file.text()
    });
    if (!response.ok) {
      let message = "Import failed.";
      try {
        const body = await response.json();
        if (body?.message) message = body.message;
      } catch {
        // ignore
      }
      throw new ApiError(message, response.status);
    }
    return (await response.json()) as T;
  }
};

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error) return error.message;
  return "Something went wrong.";
}
