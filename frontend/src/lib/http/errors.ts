/**
 * Estructura estándar RFC 7807 (ProblemDetails) retornada por la API de Yusay.
 */
export interface ApiProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

/**
 * Error base para fallas HTTP originadas en la comunicación con el backend .NET.
 */
export class ApiClientError extends Error {
  readonly status: number;
  readonly problemDetails?: ApiProblemDetails;

  constructor(status: number, message: string, problemDetails?: ApiProblemDetails) {
    super(message);
    this.name = "ApiClientError";
    this.status = status;
    this.problemDetails = problemDetails;
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

export class ValidationError extends ApiClientError {
  constructor(message = "Los datos proporcionados no son válidos.", problemDetails?: ApiProblemDetails) {
    super(400, message, problemDetails);
    this.name = "ValidationError";
  }
}

export class UnauthorizedError extends ApiClientError {
  constructor(message = "Sesión no válida o expirada.", problemDetails?: ApiProblemDetails) {
    super(401, message, problemDetails);
    this.name = "UnauthorizedError";
  }
}

export class ForbiddenError extends ApiClientError {
  constructor(message = "No cuenta con los privilegios necesarios para esta acción.", problemDetails?: ApiProblemDetails) {
    super(403, message, problemDetails);
    this.name = "ForbiddenError";
  }
}

export class NotFoundError extends ApiClientError {
  constructor(message = "El recurso solicitado no fue encontrado.", problemDetails?: ApiProblemDetails) {
    super(404, message, problemDetails);
    this.name = "NotFoundError";
  }
}

export class ConflictError extends ApiClientError {
  constructor(message = "Existe un conflicto con el estado actual del recurso.", problemDetails?: ApiProblemDetails) {
    super(409, message, problemDetails);
    this.name = "ConflictError";
  }
}

export class ServiceUnavailableError extends ApiClientError {
  constructor(message = "El servicio o la base de datos se encuentran temporalmente no disponibles.", problemDetails?: ApiProblemDetails) {
    super(503, message, problemDetails);
    this.name = "ServiceUnavailableError";
  }
}
