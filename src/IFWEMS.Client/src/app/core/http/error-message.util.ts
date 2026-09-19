import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts a human-readable message from an HttpErrorResponse. Handles the various
 * shapes returned by the IFWEMS API:
 *  - { message: string }               (custom BadRequest/Conflict payloads)
 *  - ValidationProblemDetails          ({ title, errors: { field: string[] } })
 *  - ProblemDetails                    ({ title, detail })
 *  - plain string body
 *  - network/CORS failures (status 0)
 *  - Blob bodies (file download endpoints using responseType: 'blob')
 */
export function extractErrorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Unable to reach the server. Please check your connection and try again.';
  }

  const body = parseErrorBody(error.error);

  if (typeof body === 'string' && body.trim()) {
    return body;
  }

  if (body && typeof body === 'object') {
    if (typeof (body as any).message === 'string' && (body as any).message.trim()) {
      return (body as any).message;
    }

    const errors = (body as any).errors;
    if (errors && typeof errors === 'object') {
      const messages = Object.values(errors)
        .flat()
        .filter((m): m is string => typeof m === 'string' && m.trim().length > 0);
      if (messages.length) {
        return messages.join(' ');
      }
    }

    if (typeof (body as any).detail === 'string' && (body as any).detail.trim()) {
      return (body as any).detail;
    }

    if (typeof (body as any).title === 'string' && (body as any).title.trim()) {
      return (body as any).title;
    }
  }

  if (error.status === 401) {
    return 'You are not authorized. Please sign in again.';
  }

  if (error.status === 403) {
    return 'You do not have permission to perform this action.';
  }

  if (error.status === 404) {
    return 'The requested resource could not be found.';
  }

  if (error.status === 429) {
    return 'Too many requests. Please wait a moment and try again.';
  }

  if (error.status >= 500) {
    return 'An unexpected server error occurred. Please try again later.';
  }

  return error.message || `Request failed with status ${error.status}.`;
}

function parseErrorBody(raw: unknown): unknown {
  if (typeof raw === 'string' && raw.trim().startsWith('{')) {
    try {
      return JSON.parse(raw);
    } catch {
      return raw;
    }
  }
  return raw;
}
