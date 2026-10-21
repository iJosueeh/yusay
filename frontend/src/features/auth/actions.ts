"use server";

import { headers } from "next/headers";
import { redirect } from "next/navigation";
import { backendFetch } from "@/lib/http";
import {
  UnauthorizedError,
  ValidationError,
  ConflictError,
  ServiceUnavailableError,
} from "@/lib/http/errors";
import { setSessionCookie } from "@/lib/session";
import {
  SignInResponse,
  SignInActionResult,
  RegisterResponse,
  RegisterActionResult,
  VerifyEmailResponse,
  VerifyEmailResult,
  RequestPasswordResetResponse,
  RequestPasswordResetActionResult,
  ResetPasswordResponse,
  ResetPasswordActionResult,
} from "./types";

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export async function signInAction(
  _prevState: SignInActionResult | null,
  formData: FormData
): Promise<SignInActionResult> {
  // Verificación básica de origen para mitigación CSRF en peticiones mutables
  const headerList = await headers();
  const origin = headerList.get("origin");
  const host = headerList.get("host");

  if (origin && host) {
    try {
      const originHost = new URL(origin).host;
      if (originHost !== host) {
        return {
          success: false,
          error: "Solicitud no permitida por razones de seguridad (origen no verificado).",
        };
      }
    } catch {
      return {
        success: false,
        error: "Solicitud rechazada por cabeceras inválidas.",
      };
    }
  }

  const rawEmail = formData.get("email");
  const rawPassword = formData.get("password");

  const email = typeof rawEmail === "string" ? rawEmail.trim() : "";
  // Conservar la contraseña exacta sin recortar espacios que el usuario haya introducido deliberadamente
  const password = typeof rawPassword === "string" ? rawPassword : "";

  const fieldErrors: { email?: string; password?: string } = {};

  if (!email) {
    fieldErrors.email = "Introduce tu correo electrónico.";
  } else if (!EMAIL_REGEX.test(email)) {
    fieldErrors.email = "Introduce un correo electrónico válido.";
  }

  if (!password) {
    fieldErrors.password = "Introduce tu contraseña.";
  }

  if (Object.keys(fieldErrors).length > 0) {
    return {
      success: false,
      fieldErrors,
    };
  }

  let shouldRedirect = false;

  try {
    const result = await backendFetch<SignInResponse>("/auth/sign-in", {
      method: "POST",
      body: JSON.stringify({ email, password }),
      cache: "no-store",
    });

    // Fijar la cookie HttpOnly en el servidor con el TTL sincronizado con el JWT
    await setSessionCookie(result.accessToken, result.expiresAt);
    shouldRedirect = true;
  } catch (error) {
    if (error instanceof UnauthorizedError) {
      const detail = error.problemDetails?.detail?.toLowerCase() || "";

      if (detail.includes("verificar") || detail.includes("verificado")) {
        return {
          success: false,
          error:
            "Tu cuenta aún no ha sido verificada. Por favor, confirma el enlace enviado a tu correo.",
        };
      }

      if (detail.includes("bloqueada")) {
        return {
          success: false,
          error:
            "La cuenta no se encuentra disponible para iniciar sesión. Contacta al soporte si crees que es un error.",
        };
      }

      // Mensaje genérico seguro ante credenciales no válidas
      return {
        success: false,
        error: "El correo electrónico o la contraseña ingresados no son correctos.",
      };
    }

    if (error instanceof ValidationError) {
      return {
        success: false,
        error:
          error.problemDetails?.detail ||
          "Por favor, revisa que los datos ingresados tengan el formato requerido.",
      };
    }

    if (error instanceof ServiceUnavailableError) {
      return {
        success: false,
        error:
          "El servicio de autenticación no está disponible en este momento. Por favor, intenta de nuevo en unos minutos.",
      };
    }

    return {
      success: false,
      error:
        "Ocurrió un error inesperado al procesar el inicio de sesión. Por favor, intenta más tarde.",
    };
  }

  if (shouldRedirect) {
    redirect("/inicio");
  }

  return { success: true };
}

export async function registerAction(
  _prevState: RegisterActionResult | null,
  formData: FormData
): Promise<RegisterActionResult> {
  // Verificación básica de origen para mitigación CSRF en peticiones mutables
  const headerList = await headers();
  const origin = headerList.get("origin");
  const host = headerList.get("host");

  if (origin && host) {
    try {
      const originHost = new URL(origin).host;
      if (originHost !== host) {
        return {
          success: false,
          error: "Solicitud no permitida por razones de seguridad (origen no verificado).",
        };
      }
    } catch {
      return {
        success: false,
        error: "Solicitud rechazada por cabeceras inválidas.",
      };
    }
  }

  const rawEmail = formData.get("email");
  const rawPassword = formData.get("password");
  const rawConfirmPassword = formData.get("confirmPassword");
  const rawAdultConfirmed = formData.get("adultConfirmed");

  const email = typeof rawEmail === "string" ? rawEmail.trim() : "";
  // Conservar las contraseñas sin alterar los caracteres introducidos
  const password = typeof rawPassword === "string" ? rawPassword : "";
  const confirmPassword = typeof rawConfirmPassword === "string" ? rawConfirmPassword : "";
  const adultConfirmed = rawAdultConfirmed === "on" || rawAdultConfirmed === "true";

  const fieldErrors: RegisterActionResult["fieldErrors"] = {};

  if (!email) {
    fieldErrors.email = "Introduce tu correo electrónico.";
  } else if (!EMAIL_REGEX.test(email)) {
    fieldErrors.email = "Por favor, introduce un correo electrónico válido.";
  }

  if (!password) {
    fieldErrors.password = "Introduce tu contraseña.";
  } else if (password.length < 8) {
    fieldErrors.password = "La contraseña debe contener al menos 8 caracteres.";
  }

  if (!confirmPassword) {
    fieldErrors.confirmPassword = "Confirma tu contraseña.";
  } else if (password !== confirmPassword) {
    fieldErrors.confirmPassword = "Las contraseñas no coinciden. Verifícalas con cuidado.";
  }

  if (!adultConfirmed) {
    fieldErrors.adultConfirmed = "Debes confirmar que tienes 18 años o más para continuar.";
  }

  if (Object.keys(fieldErrors).length > 0) {
    return {
      success: false,
      fieldErrors,
      values: { email, adultConfirmed },
    };
  }

  let shouldRedirect = false;

  try {
    await backendFetch<RegisterResponse>("/auth/register", {
      method: "POST",
      body: JSON.stringify({
        email,
        password,
        adultConfirmed: true,
      }),
      cache: "no-store",
    });

    shouldRedirect = true;
  } catch (error) {
    if (error instanceof ConflictError) {
      return {
        success: false,
        error:
          "Ya existe una cuenta asociada a este correo electrónico. Puedes iniciar sesión o solicitar restablecer tu contraseña.",
        values: { email, adultConfirmed },
      };
    }

    if (error instanceof ValidationError) {
      return {
        success: false,
        error:
          error.problemDetails?.detail ||
          "Por favor, revisa que los datos ingresados tengan el formato requerido.",
        values: { email, adultConfirmed },
      };
    }

    if (error instanceof ServiceUnavailableError) {
      return {
        success: false,
        error:
          "El servicio de registro no está disponible en este momento. Por favor, intenta de nuevo en unos minutos.",
        values: { email, adultConfirmed },
      };
    }

    return {
      success: false,
      error:
        "Ocurrió un error inesperado al procesar el registro. Por favor, intenta más tarde.",
      values: { email, adultConfirmed },
    };
  }

  if (shouldRedirect) {
    redirect("/auth/verificar-correo");
  }

  return { success: true };
}

export async function verifyEmailToken(
  token: string | undefined | null
): Promise<VerifyEmailResult> {
  const sanitizedToken = typeof token === "string" ? token.trim() : "";

  if (!sanitizedToken) {
    return { state: "pending" };
  }

  try {
    const result = await backendFetch<VerifyEmailResponse>("/auth/verify-email", {
      method: "POST",
      body: JSON.stringify({ token: sanitizedToken }),
      cache: "no-store",
    });

    return {
      state: "verified",
      email: result.email,
    };
  } catch {
    // Si el token no existe, expiró o ya fue consumido, mapear a 'expired'
    // sin exponer detalles internos ni el token en logs
    return {
      state: "expired",
    };
  }
}

export async function requestPasswordResetAction(
  _prevState: RequestPasswordResetActionResult | null,
  formData: FormData
): Promise<RequestPasswordResetActionResult> {
  const headerList = await headers();
  const origin = headerList.get("origin");
  const host = headerList.get("host");

  if (origin && host) {
    try {
      const originHost = new URL(origin).host;
      if (originHost !== host) {
        return {
          success: false,
          error: "Solicitud no permitida por razones de seguridad (origen no verificado).",
        };
      }
    } catch {
      return {
        success: false,
        error: "Solicitud rechazada por cabeceras inválidas.",
      };
    }
  }

  const rawEmail = formData.get("email");
  const email = typeof rawEmail === "string" ? rawEmail.trim() : "";

  if (!email) {
    return {
      success: false,
      fieldErrors: { email: "Introduce tu correo electrónico." },
    };
  }

  if (!EMAIL_REGEX.test(email)) {
    return {
      success: false,
      fieldErrors: { email: "Por favor, introduce un correo electrónico válido." },
    };
  }

  try {
    await backendFetch<RequestPasswordResetResponse>("/auth/password-reset/request", {
      method: "POST",
      body: JSON.stringify({ email }),
      cache: "no-store",
    });

    // Respuesta anti-enumeración estricta
    return {
      success: true,
      email,
    };
  } catch (error) {
    if (error instanceof ServiceUnavailableError) {
      return {
        success: false,
        error:
          "El servicio de recuperación no está disponible en este momento. Por favor, intenta de nuevo en unos minutos.",
      };
    }

    return {
      success: false,
      error: "Ocurrió un error inesperado al procesar la solicitud. Por favor, intenta más tarde.",
    };
  }
}

export async function resetPasswordAction(
  _prevState: ResetPasswordActionResult | null,
  formData: FormData
): Promise<ResetPasswordActionResult> {
  const headerList = await headers();
  const origin = headerList.get("origin");
  const host = headerList.get("host");

  if (origin && host) {
    try {
      const originHost = new URL(origin).host;
      if (originHost !== host) {
        return {
          success: false,
          error: "Solicitud no permitida por razones de seguridad (origen no verificado).",
        };
      }
    } catch {
      return {
        success: false,
        error: "Solicitud rechazada por cabeceras inválidas.",
      };
    }
  }

  const rawToken = formData.get("token");
  const rawNewPassword = formData.get("newPassword");
  const rawConfirmNewPassword = formData.get("confirmNewPassword");

  const token = typeof rawToken === "string" ? rawToken.trim() : "";
  const newPassword = typeof rawNewPassword === "string" ? rawNewPassword : "";
  const confirmNewPassword = typeof rawConfirmNewPassword === "string" ? rawConfirmNewPassword : "";

  if (!token) {
    return {
      success: false,
      isExpiredToken: true,
      error: "El enlace de recuperación es inválido o ha vencido. Por favor, solicita uno nuevo.",
    };
  }

  const fieldErrors: ResetPasswordActionResult["fieldErrors"] = {};

  if (!newPassword) {
    fieldErrors.newPassword = "Introduce una nueva contraseña.";
  } else if (newPassword.length < 8) {
    fieldErrors.newPassword = "La contraseña debe contener al menos 8 caracteres.";
  }

  if (!confirmNewPassword) {
    fieldErrors.confirmNewPassword = "Confirma la nueva contraseña.";
  } else if (newPassword !== confirmNewPassword) {
    fieldErrors.confirmNewPassword = "Las contraseñas no coinciden o tienen menos de 8 caracteres.";
  }

  if (Object.keys(fieldErrors).length > 0) {
    return {
      success: false,
      fieldErrors,
      error: "Las contraseñas no coinciden o tienen menos de 8 caracteres.",
      values: { newPassword, confirmNewPassword },
    };
  }

  try {
    await backendFetch<ResetPasswordResponse>("/auth/password-reset/confirm", {
      method: "POST",
      body: JSON.stringify({
        token,
        newPassword,
      }),
      cache: "no-store",
    });

    return {
      success: true,
    };
  } catch (error) {
    if (
      error instanceof ValidationError ||
      (error as { status?: number }).status === 400 ||
      (error as { status?: number }).status === 404
    ) {
      return {
        success: false,
        isExpiredToken: true,
        error: "Este enlace no es válido o ha vencido.",
        values: { newPassword, confirmNewPassword },
      };
    }

    if (error instanceof ServiceUnavailableError) {
      return {
        success: false,
        error: "El servicio no se encuentra disponible temporalmente. Por favor, intenta de nuevo.",
        values: { newPassword, confirmNewPassword },
      };
    }

    return {
      success: false,
      error: "Ocurrió un error inesperado al actualizar la contraseña. Por favor, intenta más tarde.",
      values: { newPassword, confirmNewPassword },
    };
  }
}
