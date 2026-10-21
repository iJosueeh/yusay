"use client";

import { useActionState, useState } from "react";
import Link from "next/link";
import { Loader2, AlertCircle, CheckCircle2, Mail, ArrowRight, ShieldCheck } from "lucide-react";
import { registerAction } from "../actions";
import { FormInput } from "./form-input";
import { PasswordInput } from "./password-input";
import { FormAlert } from "./form-alert";

export function RegisterForm() {
  const [state, formAction, isPending] = useActionState(registerAction, null);
  const [adultConfirmed, setAdultConfirmed] = useState(false);

  // Pantalla de cuenta creada y verificación pendiente (tras recibir respuesta exitosa 201)
  if (state?.success) {
    return (
      <div className="w-full text-center space-y-6">
        <div className="w-14 h-14 rounded-full bg-warm-stone mx-auto flex items-center justify-center text-forest-depths shadow-none">
          <CheckCircle2 className="h-7 w-7 text-forest-depths" aria-hidden="true" />
        </div>

        <div className="space-y-2">
          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-lime-pulse text-forest-depths font-mono text-[0.6875rem] uppercase tracking-wider font-medium">
            Cuenta creada con éxito
          </span>
          <h2 className="font-serif text-2xl md:text-3xl text-forest-depths font-light tracking-tight">
            Verificación pendiente
          </h2>
          <p className="text-sm text-pewter font-sans leading-relaxed max-w-sm mx-auto">
            Hemos registrado tu espacio confidencial asociado a{" "}
            <strong className="font-semibold text-forest-depths">{state.email}</strong>.
          </p>
        </div>

        <div
          className="p-5 rounded-2xl bg-warm-stone border border-frosted-glass text-forest-depths text-left space-y-2.5 shadow-none"
          role="status"
        >
          <div className="flex items-start gap-3">
            <Mail className="w-5 h-5 text-forest-depths shrink-0 mt-0.5" aria-hidden="true" />
            <div className="text-xs md:text-sm text-forest-depths leading-relaxed font-sans space-y-1.5">
              <p className="font-medium text-forest-depths">
                Activación mediante confirmación de correo
              </p>
              <p className="text-pewter">
                Para proteger tu privacidad y asegurar que tu espacio solo sea accesible por ti, es necesario verificar la dirección de correo antes de iniciar sesión.
              </p>
              <p className="text-[0.75rem] text-pewter/80 italic pt-1 border-t border-frosted-glass/50">
                Aviso: Los servicios de despacho de correo se encuentran en etapa de verificación y auditoría técnica. Tan pronto tu cuenta quede confirmada podrás ingresar sin inconvenientes.
              </p>
            </div>
          </div>
        </div>

        <div className="pt-2 space-y-3">
          <Link
            href="/auth/login"
            className="w-full bg-forest-depths hover:opacity-90 text-snow-white text-sm font-medium py-4 px-6 rounded-full transition duration-200 shadow-none flex items-center justify-center gap-2 active:scale-[0.99] select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/50 cursor-pointer"
          >
            <span>Ir a iniciar sesión</span>
            <ArrowRight className="w-4 h-4" aria-hidden="true" />
          </Link>

          <Link
            href="/"
            className="inline-block text-xs md:text-sm text-pewter hover:text-forest-depths transition-colors py-1.5 font-sans"
          >
            Volver a la página principal
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="w-full">
      {/* Banner de error general (conflicto 409, validación o error de servicio) */}
      {state?.error && (
        <div className="mb-6">
          <FormAlert variant="error" message={state.error} />
        </div>
      )}

      {/* Formulario conectado al BFF */}
      <form action={formAction} noValidate className="space-y-4">
        {/* Correo electrónico */}
        <FormInput
          label="Correo electrónico"
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          placeholder="nombre@ejemplo.com"
          icon={Mail}
          defaultValue={state?.values?.email ?? ""}
          disabled={isPending}
          error={state?.fieldErrors?.email}
          required
        />

        {/* Contraseña */}
        <PasswordInput
          label="Contraseña"
          id="password"
          name="password"
          autoComplete="new-password"
          placeholder="••••••••"
          minLength={8}
          cornerHint="Mínimo 8 caracteres"
          disabled={isPending}
          error={state?.fieldErrors?.password}
          required
        />

        {/* Confirmar contraseña */}
        <PasswordInput
          label="Confirmar contraseña"
          id="confirmPassword"
          name="confirmPassword"
          autoComplete="new-password"
          placeholder="••••••••"
          minLength={8}
          disabled={isPending}
          error={state?.fieldErrors?.confirmPassword}
          required
        />

        {/* Casilla de verificación de edad (+18) con radio de 8px */}
        <div className="pt-1 text-left">
          <div
            className={`p-3.5 rounded-lg border transition-colors shadow-none ${
              state?.fieldErrors?.adultConfirmed
                ? "border-terracotta bg-terracotta-light/30"
                : "border-frosted-glass bg-snow-white hover:border-ash"
            }`}
          >
            <label className="flex items-start gap-3 cursor-pointer group select-none">
              <input
                key="adultConfirmed"
                type="checkbox"
                id="adultConfirmed"
                name="adultConfirmed"
                required
                checked={adultConfirmed}
                onChange={(e) => setAdultConfirmed(e.target.checked)}
                disabled={isPending}
                className="mt-1 h-4 w-4 rounded border-frosted-glass text-forest-depths accent-forest-depths focus:ring-forest-depths/30 cursor-pointer"
              />
              <div className="flex flex-col">
                <span className="text-sm text-forest-depths leading-snug group-hover:opacity-85 transition-opacity font-sans font-medium">
                  Confirmo que tengo 18 años o más
                </span>
                <span className="text-xs text-pewter mt-0.5 font-sans">
                  Yusay está concebido exclusivamente para personas adultas.
                </span>
              </div>
            </label>
          </div>
          {state?.fieldErrors?.adultConfirmed && (
            <p className="text-xs text-terracotta mt-1.5 pl-2 flex items-center gap-1 font-sans font-medium">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" aria-hidden="true" />
              <span>{state.fieldErrors.adultConfirmed}</span>
            </p>
          )}
        </div>

        {/* Botón de envío (1000px pill, Forest Depths) */}
        <div className="pt-2">
          <button
            type="submit"
            disabled={isPending}
            className="w-full bg-forest-depths hover:opacity-90 text-snow-white text-sm font-medium py-4 px-6 rounded-full transition duration-200 shadow-none flex items-center justify-center gap-2 active:scale-[0.99] disabled:bg-ash disabled:text-snow-white disabled:cursor-not-allowed select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/50 cursor-pointer"
          >
            {isPending ? (
              <>
                <Loader2
                  className="animate-spin -ml-1 mr-2 h-4 w-4 text-snow-white"
                  aria-hidden="true"
                />
                <span>Creando cuenta...</span>
              </>
            ) : (
              <span>Crear cuenta</span>
            )}
          </button>
        </div>
      </form>

      {/* Separador y enlace secundario */}
      <div className="mt-7 pt-5 border-t border-warm-stone text-center relative">
        <p className="text-sm text-pewter font-sans">
          ¿Ya tienes una cuenta?
          <Link
            href="/auth/login"
            className="font-medium text-forest-depths hover:underline underline-offset-4 ml-1 transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-forest-depths/40 rounded"
          >
            Iniciar sesión
          </Link>
        </p>
      </div>

      {/* Micro-sello de confidencialidad y privacidad */}
      <div className="mt-5 pt-4 border-t border-warm-stone flex items-center justify-center gap-1.5 text-center text-pewter">
        <ShieldCheck className="w-3.5 h-3.5 text-forest-depths shrink-0" aria-hidden="true" />
        <p className="text-xs text-pewter leading-relaxed font-sans">
          Registro confidencial: no solicitamos tu nombre ni datos personales.
        </p>
      </div>
    </div>
  );
}
