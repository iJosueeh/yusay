import "server-only";
import { serverConfig } from "@/lib/config";
import {
  ApiClientError,
  ApiProblemDetails,
  ConflictError,
  ForbiddenError,
  NotFoundError,
  ServiceUnavailableError,
  UnauthorizedError,
  ValidationError,
} from "./errors";

export interface BackendRequestOptions extends RequestInit {
  authToken?: string;
}

/**
 * Cliente HTTP server-only reutilizable para comunicar el BFF de Next.js
 * con la API de Yusay (.NET).
 *
 * Características:
 * - Aislamiento server-only (nunca ejecutable en cliente navegador).
 * - Desactiva caché de servidor por defecto (`cache: "no-store"`) para prevenir fugas de datos.
 * - Mapeo centralizado de respuestas RFC 7807 (ProblemDetails) a errores tipados.
 */
export async function backendFetch<T>(
  path: string,
  options: BackendRequestOptions = {}
): Promise<T> {
  const { authToken, headers, cache = "no-store", ...restOptions } = options;

  const url = `${serverConfig.backendApiUrl}${path.startsWith("/") ? path : `/${path}`}`;

  const requestHeaders = new Headers(headers);

  if (!requestHeaders.has("Accept")) {
    requestHeaders.set("Accept", "application/json, application/problem+json");
  }

  if (options.body && typeof options.body === "string" && !requestHeaders.has("Content-Type")) {
    requestHeaders.set("Content-Type", "application/json");
  }

  if (authToken) {
    requestHeaders.set("Authorization", `Bearer ${authToken}`);
  }

  let response: Response;
  try {
    response = await fetch(url, {
      ...restOptions,
      headers: requestHeaders,
      cache,
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Error de red";
    throw new ServiceUnavailableError(
      `No fue posible establecer conexión con el backend de Yusay: ${message}`
    );
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }

  const contentType = response.headers.get("content-type") || "";
  const isJson = contentType.includes("application/json") || contentType.includes("application/problem+json");

  if (!response.ok) {
    let problemDetails: ApiProblemDetails | undefined;
    let errorMessage = `Error HTTP ${response.status} recibido del backend.`;

    if (isJson) {
      try {
        problemDetails = (await response.json()) as ApiProblemDetails;
        if (problemDetails?.detail) {
          errorMessage = problemDetails.detail;
        } else if (problemDetails?.title) {
          errorMessage = problemDetails.title;
        }
      } catch {
        // Fallback al mensaje por defecto si el body no era JSON válido
      }
    } else {
      const text = await response.text().catch(() => "");
      if (text) {
        errorMessage = text;
      }
    }

    switch (response.status) {
      case 400:
        throw new ValidationError(errorMessage, problemDetails);
      case 401:
        throw new UnauthorizedError(errorMessage, problemDetails);
      case 403:
        throw new ForbiddenError(errorMessage, problemDetails);
      case 404:
        throw new NotFoundError(errorMessage, problemDetails);
      case 409:
        throw new ConflictError(errorMessage, problemDetails);
      case 503:
        throw new ServiceUnavailableError(errorMessage, problemDetails);
      default:
        throw new ApiClientError(response.status, errorMessage, problemDetails);
    }
  }

  if (!isJson) {
    return (await response.text()) as unknown as T;
  }

  return (await response.json()) as T;
}
