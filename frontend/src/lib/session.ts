import "server-only";
import { cookies } from "next/headers";
import { backendFetch } from "@/lib/http";
import { UnauthorizedError } from "@/lib/http/errors";

export const SESSION_COOKIE_NAME = "__session";

export interface UserSessionProfile {
  userId: string;
  email: string;
  emailVerified: boolean;
  createdAt: string;
  adultConfirmedAt: string;
  status: string;
}

/**
 * Almacena el token de acceso JWT en una cookie HttpOnly segura.
 * La expiración se sincroniza estrictamente con el vencimiento del JWT
 * (máximo 3600 segundos / 60 minutos).
 */
export async function setSessionCookie(
  accessToken: string,
  expiresAt: string | Date
): Promise<void> {
  const expiresAtMs =
    typeof expiresAt === "string"
      ? new Date(expiresAt).getTime()
      : expiresAt.getTime();
  const secondsRemaining = Math.floor((expiresAtMs - Date.now()) / 1000);

  // Asegura un TTL positivo y acotado al límite del backend (3600s)
  const maxAge = Math.min(Math.max(0, secondsRemaining), 3600);

  const cookieStore = await cookies();
  cookieStore.set(SESSION_COOKIE_NAME, accessToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge,
  });
}

/**
 * Obtiene el token de sesión actual desde la cookie HttpOnly.
 */
export async function getSessionToken(): Promise<string | null> {
  const cookieStore = await cookies();
  return cookieStore.get(SESSION_COOKIE_NAME)?.value || null;
}

/**
 * Elimina la cookie de sesión del navegador.
 */
export async function clearSessionCookie(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(SESSION_COOKIE_NAME);
}

/**
 * Valida de forma real la sesión del usuario contra el backend .NET (`GET /users/me`).
 * No se limita a verificar la presencia de la cookie: comprueba vigencia del JWT
 * y que el token no haya sido revocado en la denylist de Redis.
 *
 * Retorna el perfil del usuario si es válida, o `null` si es inválida/expirada.
 */
export async function validateSession(): Promise<UserSessionProfile | null> {
  const token = await getSessionToken();
  if (!token) {
    return null;
  }

  try {
    const profile = await backendFetch<UserSessionProfile>("/users/me", {
      authToken: token,
      cache: "no-store",
    });
    return profile;
  } catch (error) {
    if (error instanceof UnauthorizedError) {
      // Sesión expirada o token revocado en Redis: limpiar cookie
      await clearSessionCookie();
    }
    return null;
  }
}
