"use client";

import { useActionState } from "react";
import Link from "next/link";
import { Loader2, Mail, ArrowLeft } from "lucide-react";
import { requestPasswordResetAction } from "../actions";
import { FormInput } from "./form-input";
import { FormAlert } from "./form-alert";

export function RequestPasswordResetForm() {
  const [state, formAction, isPending] = useActionState(
    requestPasswordResetAction,
    null
  );

  // Respuesta neutral anti-enumeración
  if (state?.success) {
    return (
      <div className="w-full text-center space-y-6">
        <div className="w-14 h-14 rounded-full bg-sage-light/80 ring-8 ring-sage-subtle mx-auto flex items-center justify-center text-forest shadow-xs">
          <Mail className="h-7 w-7 text-forest" aria-hidden="true" />
        </div>

        <div className="space-y-2">
          <div className="inline-flex items-center gap-2 mb-1 px-3 py-1 rounded-full bg-sage-subtle border border-sage/40 w-fit mx-auto">
            <span className="w-1.5 h-1.5 rounded-full bg-forest" aria-hidden="true" />
            <span className="text-[0.6875rem] font-semibold uppercase tracking-[0.2em] text-forest block font-sans">
              Solicitud procesada
            </span>
          </div>

          <h2 className="font-serif text-2xl md:text-3xl text-midnight font-normal tracking-tight">
            Revisa las indicaciones
          </h2>
          <p className="text-sm text-midnight-muted font-sans leading-relaxed max-w-sm mx-auto">
            Si la dirección ingresada corresponde a una cuenta registrada en Yusay,
            se generarán las instrucciones para restablecer tu contraseña.
          </p>
        </div>

        <div
          className="p-4 rounded-xl bg-[#F0F5F2] border border-forest/20 text-midnight text-left space-y-2.5 transition-all"
          role="status"
        >
          <p className="text-xs md:text-sm text-midnight/90 leading-relaxed font-sans">
            Por estrictas razones de privacidad y protección de tu espacio, no confirmamos públicamente si una cuenta existe o no en nuestro sistema.
          </p>
          <p className="text-[0.75rem] text-midnight-subtle italic pt-2 border-t border-sage/20 font-sans">
            Aviso: Los servicios de despacho de correo se encuentran en etapa de verificación y auditoría técnica. Tan pronto tu enlace sea generado podrás continuar con el restablecimiento de tus credenciales.
          </p>
        </div>

        <div className="pt-2 space-y-3">
          <Link
            href="/auth/login"
            className="w-full bg-forest hover:bg-forest-hover text-canvas text-xs md:text-sm font-semibold uppercase tracking-[0.14em] py-3.5 px-6 rounded-xl transition duration-200 shadow-sm flex items-center justify-center gap-2 active:scale-[0.99] select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
          >
            <ArrowLeft className="w-4 h-4" aria-hidden="true" />
            <span>Volver a iniciar sesión</span>
          </Link>

          <Link
            href="/"
            className="inline-block text-xs md:text-sm text-midnight-muted hover:text-forest transition-colors py-1.5 font-sans"
          >
            Regresar a la portada
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="w-full">
      {/* Banner de error general */}
      {state?.error && (
        <div className="mb-6">
          <FormAlert variant="error" message={state.error} />
        </div>
      )}

      {/* Formulario conectado al BFF */}
      <form action={formAction} noValidate className="space-y-4">
        <FormInput
          label="Correo electrónico"
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          placeholder="nombre@ejemplo.com"
          icon={Mail}
          disabled={isPending}
          error={state?.fieldErrors?.email}
          required
        />

        <div className="pt-2">
          <button
            type="submit"
            disabled={isPending}
            className="w-full bg-forest hover:bg-forest-hover text-canvas text-xs md:text-sm font-semibold uppercase tracking-[0.14em] py-3.5 px-6 rounded-xl transition duration-200 shadow-sm flex items-center justify-center gap-2 active:scale-[0.99] disabled:opacity-80 disabled:cursor-not-allowed select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
          >
            {isPending ? (
              <>
                <Loader2
                  className="animate-spin -ml-1 mr-2 h-4 w-4 text-canvas"
                  aria-hidden="true"
                />
                <span>Enviando instrucciones...</span>
              </>
            ) : (
              <span>Enviar instrucciones</span>
            )}
          </button>
        </div>
      </form>

      {/* Separador con motivo central y enlace de retorno */}
      <div className="mt-8 pt-6 border-t border-sage/30 text-center relative">
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
