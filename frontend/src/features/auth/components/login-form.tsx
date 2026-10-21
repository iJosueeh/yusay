"use client";

import { useActionState } from "react";
import Link from "next/link";
import { Loader2, Mail, ShieldCheck } from "lucide-react";
import { signInAction } from "../actions";
import { FormInput } from "./form-input";
import { PasswordInput } from "./password-input";
import { FormAlert } from "./form-alert";

interface LoginFormProps {
  sessionExpired?: boolean;
}

export function LoginForm({ sessionExpired = false }: LoginFormProps) {
  const [state, formAction, isPending] = useActionState(signInAction, null);

  return (
    <div className="w-full">
      {/* Banner de estado / expiración / error */}
      {sessionExpired && !state?.error && (
        <div className="mb-6">
          <FormAlert
            variant="info"
            message="Tu sesión de 60 minutos ha expirado o ya no es válida. Por favor, ingresa tus credenciales para continuar explorando tu espacio."
          />
        </div>
      )}

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

        <PasswordInput
          label="Contraseña"
          id="password"
          name="password"
          autoComplete="current-password"
          placeholder="••••••••"
          forgotPasswordHref="/auth/recuperar-contrasena"
          disabled={isPending}
          error={state?.fieldErrors?.password}
          required
        />

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
                <span>Iniciando sesión...</span>
              </>
            ) : (
              <span>Iniciar sesión</span>
            )}
          </button>
        </div>
      </form>

      {/* Separador y enlace secundario */}
      <div className="mt-8 pt-6 border-t border-warm-stone text-center relative">
        <p className="text-sm text-pewter font-sans">
          ¿Aún no tienes una cuenta?
          <Link
            href="/auth/registro"
            className="font-medium text-forest-depths hover:underline underline-offset-4 ml-1 transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-forest-depths/40 rounded"
          >
            Crear cuenta
          </Link>
        </p>
      </div>

      {/* Micro-sello de confidencialidad */}
      <div className="mt-5 pt-4 border-t border-warm-stone flex items-center justify-center gap-1.5 text-pewter">
        <ShieldCheck className="w-3.5 h-3.5 text-forest-depths" aria-hidden="true" />
        <span className="text-[0.6875rem] font-sans">
          Tus registros se conservan confidenciales y privados
        </span>
      </div>
    </div>
  );
}
