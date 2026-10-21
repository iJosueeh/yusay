"use client";

import { useActionState } from "react";
import Link from "next/link";
import { Loader2, AlertTriangle, CheckCircle2, ArrowLeft, Clock } from "lucide-react";
import { resetPasswordAction } from "../actions";
import { PasswordInput } from "./password-input";
import { TokenUrlScrubber } from "./email-verification-view";

export interface ResetPasswordFormProps {
  token?: string;
}

export function ResetPasswordForm({ token = "" }: ResetPasswordFormProps) {
  const [state, formAction, isPending] = useActionState(resetPasswordAction, null);

  const hasToken = Boolean(token && token.trim().length > 0);

  // Estado: Token ausente o enlace vencido / inválido
  if (!hasToken || state?.isExpiredToken) {
    return (
      <div className="flex flex-col text-left py-2">
        {hasToken && <TokenUrlScrubber />}

        <div className="w-14 h-14 rounded-full bg-terracotta-light ring-8 ring-terracotta-light/40 flex items-center justify-center mb-5 text-terracotta border border-terracotta/20 shadow-xs">
          <Clock className="w-6 h-6" aria-hidden="true" />
        </div>

        <div className="inline-flex items-center gap-2 mb-2 px-3 py-1 rounded-full bg-terracotta-light/60 border border-terracotta/20 w-fit">
          <span className="w-1.5 h-1.5 rounded-full bg-terracotta" aria-hidden="true" />
          <span className="text-[0.6875rem] font-semibold uppercase tracking-[0.2em] text-terracotta block font-sans">
            Enlace caducado
          </span>
        </div>

        <h2 className="font-serif text-3xl md:text-[2.125rem] text-midnight tracking-tight font-normal mb-3 leading-tight">
          Este enlace no es válido o ha vencido
        </h2>

        <p className="text-sm text-midnight-muted leading-relaxed mb-6 font-sans">
          Los enlaces de recuperación tienen una duración de 30 minutos por seguridad y protección de tu espacio. Puedes solicitar un nuevo enlace en cualquier momento.
        </p>

        <div className="space-y-3 pt-2">
          <Link
            href="/auth/recuperar-contrasena"
            className="w-full bg-forest hover:bg-forest-hover text-canvas text-xs md:text-sm font-semibold uppercase tracking-[0.14em] py-3.5 px-6 rounded-xl transition duration-200 shadow-sm flex items-center justify-center text-center select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
          >
            Solicitar otro enlace
          </Link>

          <Link
            href="/"
            className="w-full bg-transparent hover:bg-sage-subtle text-midnight-muted hover:text-midnight border border-sage/50 text-xs md:text-sm font-semibold uppercase tracking-[0.14em] py-3 px-6 rounded-xl transition duration-200 flex items-center justify-center text-center select-none"
          >
            Volver al inicio
          </Link>
        </div>
      </div>
    );
  }

  // Estado: Éxito (Contraseña actualizada confirmada por backend)
  if (state?.success) {
    return (
      <div className="flex flex-col text-left py-2">
        <TokenUrlScrubber />

        <div className="w-14 h-14 rounded-full bg-[#F0F5F2] ring-8 ring-sage-subtle flex items-center justify-center mb-5 text-forest border border-sage/30 shadow-xs">
          <CheckCircle2 className="w-7 h-7 text-forest" aria-hidden="true" />
        </div>

        <div className="inline-flex items-center gap-2 mb-2 px-3 py-1 rounded-full bg-sage-subtle border border-sage/40 w-fit">
          <span className="w-1.5 h-1.5 rounded-full bg-forest" aria-hidden="true" />
          <span className="text-[0.6875rem] font-semibold uppercase tracking-[0.2em] text-forest block font-sans">
            Actualización completada
          </span>
        </div>

        <h2 className="font-serif text-3xl md:text-[2.125rem] text-midnight tracking-tight font-normal mb-3 leading-tight">
          Tu contraseña se actualizó correctamente
        </h2>

        <p className="text-sm text-midnight-muted leading-relaxed mb-6 font-sans">
          Ya puedes iniciar sesión con tu nueva contraseña y continuar explorando tu espacio personal de bienestar.
        </p>

        <div className="pt-2">
          <Link
            href="/auth/login"
            className="w-full bg-forest hover:bg-forest-hover text-canvas text-xs md:text-sm font-semibold uppercase tracking-[0.14em] py-3.5 px-6 rounded-xl transition duration-200 shadow-sm flex items-center justify-center text-center select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
          >
            Iniciar sesión
          </Link>
        </div>
      </div>
    );
  }

  // Estado: Formulario de nueva contraseña
  return (
    <div className="flex flex-col text-left">
      <TokenUrlScrubber />

      <div className="mb-6">
        <div className="inline-flex items-center gap-2 mb-2.5 px-3 py-1 rounded-full bg-sage-subtle/80 border border-sage/40 w-fit">
          <span className="w-1.5 h-1.5 rounded-full bg-forest" aria-hidden="true" />
          <span className="text-[0.6875rem] font-semibold uppercase tracking-[0.2em] text-midnight-muted block font-sans">
            Seguridad de la cuenta
          </span>
        </div>
        <h1 className="font-serif text-3xl md:text-[2.125rem] text-midnight tracking-tight font-normal mb-2 leading-tight">
          Crea una nueva contraseña
        </h1>
        <p className="text-sm text-midnight-muted leading-relaxed font-sans">
          Define una contraseña segura para acceder a tu espacio personal.
        </p>
      </div>

      {/* Banner de error */}
      {state?.error && (
        <div
          className="mb-5 p-4 rounded-xl bg-terracotta-light border border-terracotta/30 text-midnight transition-all duration-300"
          role="alert"
        >
          <div className="flex items-start gap-3">
            <AlertTriangle className="w-5 h-5 text-terracotta shrink-0 mt-0.5" aria-hidden="true" />
            <div className="text-xs md:text-sm text-midnight/90 leading-relaxed font-sans">
              {state.error}
            </div>
          </div>
        </div>
      )}

      <form action={formAction} noValidate className="space-y-4">
        {/* Token oculto */}
        <input type="hidden" name="token" value={token} />

        {/* Nueva contraseña */}
        <PasswordInput
          label="Nueva contraseña"
          id="newPassword"
          name="newPassword"
          autoComplete="new-password"
          placeholder="••••••••"
          minLength={8}
          cornerHint="Mínimo 8 caracteres"
          defaultValue={state?.values?.newPassword ?? ""}
          disabled={isPending}
          error={state?.fieldErrors?.newPassword}
          required
        />

        {/* Confirmar contraseña */}
        <PasswordInput
          label="Confirmar contraseña"
          id="confirmNewPassword"
          name="confirmNewPassword"
          autoComplete="new-password"
          placeholder="••••••••"
          minLength={8}
          defaultValue={state?.values?.confirmNewPassword ?? ""}
          disabled={isPending}
          error={state?.fieldErrors?.confirmNewPassword}
          required
        />

        {/* Botón de envío */}
        <div className="pt-2">
          <button
            type="submit"
            disabled={isPending}
            className="w-full bg-forest hover:bg-forest-hover text-canvas text-xs md:text-sm font-semibold uppercase tracking-[0.14em] py-3.5 px-6 rounded-xl transition duration-200 shadow-sm flex items-center justify-center gap-2 active:scale-[0.99] disabled:opacity-80 disabled:cursor-not-allowed select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
          >
            {isPending ? (
              <>
                <Loader2 className="animate-spin -ml-1 mr-2 h-4 w-4 text-canvas" aria-hidden="true" />
                <span>Guardando nueva contraseña...</span>
              </>
            ) : (
              <span>Guardar nueva contraseña</span>
            )}
          </button>
        </div>
      </form>

      {/* Separador con motivo central y enlace secundario inferior */}
      <div className="mt-7 pt-5 border-t border-sage/30 text-center relative">
        <div className="absolute left-1/2 -top-2.5 -translate-x-1/2 bg-white px-2 text-sage/70 text-xs select-none" aria-hidden="true">
          •
        </div>
        <Link
          href="/auth/login"
          className="inline-flex items-center gap-1.5 text-sm font-medium text-forest hover:underline decoration-sage underline-offset-4 transition-colors group focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-forest/40 rounded"
        >
          <ArrowLeft className="w-4 h-4 transition-transform group-hover:-translate-x-0.5" />
          <span>Volver a iniciar sesión</span>
        </Link>
      </div>
    </div>
  );
}
