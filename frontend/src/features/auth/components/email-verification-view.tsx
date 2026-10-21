"use client";

import { useEffect } from "react";
import Link from "next/link";
import { LogIn, CheckCircle2, Link2Off, ArrowRight, Mail } from "lucide-react";
import type { EmailVerificationState } from "../types";

export function TokenUrlScrubber() {
  useEffect(() => {
    if (typeof window !== "undefined" && window.location.search.includes("token=")) {
      const url = new URL(window.location.href);
      url.searchParams.delete("token");
      const cleanUrl =
        url.pathname + (url.searchParams.toString() ? `?${url.searchParams.toString()}` : "");
      window.history.replaceState(null, "", cleanUrl);
    }
  }, []);

  return null;
}

export interface EmailVerificationViewProps {
  state: EmailVerificationState;
  hasTokenInUrl?: boolean;
}

export function EmailVerificationView({
  state,
  hasTokenInUrl = false,
}: EmailVerificationViewProps) {
  return (
    <div className="w-full">
      {hasTokenInUrl && <TokenUrlScrubber />}

      {/* Estado 1: Pendiente */}
      {state === "pending" && (
        <div className="flex flex-col text-left">
          <div className="w-14 h-14 rounded-full bg-sage-light/80 ring-8 ring-sage-subtle flex items-center justify-center text-forest mb-5 shadow-xs">
            <Mail className="w-6 h-6" aria-hidden="true" />
          </div>

          <div className="inline-flex items-center gap-2 mb-2.5 px-3 py-1 rounded-full bg-terracotta-light/60 border border-terracotta/20 w-fit">
            <span
              className="w-2 h-2 rounded-full bg-terracotta animate-pulse"
              aria-hidden="true"
            />
            <span className="text-[0.6875rem] uppercase tracking-[0.2em] font-semibold text-terracotta font-sans">
              Verificación en curso
            </span>
          </div>

          <h1 className="font-serif text-3xl md:text-[2.125rem] text-midnight tracking-tight font-normal mt-1 leading-tight">
            Tu cuenta está casi lista
          </h1>

          <p className="text-sm text-midnight-muted mt-3 leading-relaxed font-sans">
            Para acceder a tu espacio, es necesario verificar tu dirección de correo electrónico
            antes de iniciar sesión. Por favor, revisa tu bandeja de entrada y sigue las
            indicaciones para continuar.
          </p>

          <div className="mt-8 pt-6 border-t border-sage/30 flex flex-col gap-3 relative">
            <div className="absolute left-1/2 -top-2.5 -translate-x-1/2 bg-white px-2 text-sage/70 text-xs select-none" aria-hidden="true">
              •
            </div>

            <Link
              href="/auth/login"
              className="inline-flex items-center justify-center gap-2 w-full py-3.5 px-6 rounded-xl text-xs md:text-sm font-semibold uppercase tracking-[0.14em] bg-forest text-canvas hover:bg-forest-hover transition-colors shadow-sm select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
            >
              <LogIn className="w-4.5 h-4.5" aria-hidden="true" />
              <span>Iniciar sesión</span>
            </Link>

            <Link
              href="/"
              className="text-center text-xs uppercase tracking-widest text-midnight-muted hover:text-forest transition-colors py-1.5 font-sans"
            >
              Regresar a la portada
            </Link>
          </div>
        </div>
      )}

      {/* Estado 2: Correo verificado */}
      {state === "verified" && (
        <div className="flex flex-col text-left">
          <div className="w-14 h-14 rounded-full bg-[#F0F5F2] ring-8 ring-sage-subtle flex items-center justify-center text-forest mb-5 shadow-xs border border-sage/30">
            <CheckCircle2 className="w-7 h-7 text-forest" aria-hidden="true" />
          </div>

          <div className="inline-flex items-center gap-2 mb-2.5 px-3 py-1 rounded-full bg-sage-subtle border border-sage/40 w-fit">
            <span className="w-1.5 h-1.5 rounded-full bg-forest" aria-hidden="true" />
            <span className="text-[0.6875rem] uppercase tracking-[0.2em] font-semibold text-forest font-sans">
              Completado con éxito
            </span>
          </div>

          <h1 className="font-serif text-3xl md:text-[2.125rem] text-midnight tracking-tight font-normal mt-1 leading-tight">
            Tu correo ha sido verificado
          </h1>

          <p className="text-sm text-midnight-muted mt-3 leading-relaxed font-sans">
            Tu cuenta ha sido activada correctamente. Ya puedes acceder a tu espacio personal de
            bienestar.
          </p>

          <div className="mt-8 pt-6 border-t border-sage/30 flex flex-col gap-3 relative">
            <div className="absolute left-1/2 -top-2.5 -translate-x-1/2 bg-white px-2 text-sage/70 text-xs select-none" aria-hidden="true">
              •
            </div>

            <Link
              href="/auth/login"
              className="inline-flex items-center justify-center gap-2 w-full py-3.5 px-6 rounded-xl text-xs md:text-sm font-semibold uppercase tracking-[0.14em] bg-forest text-canvas hover:bg-forest-hover transition-colors shadow-sm select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
            >
              <span>Iniciar sesión</span>
              <ArrowRight className="w-4.5 h-4.5" aria-hidden="true" />
            </Link>

            <Link
              href="/"
              className="text-center text-xs uppercase tracking-widest text-midnight-muted hover:text-forest transition-colors py-1.5 font-sans"
            >
              Regresar a la portada
            </Link>
          </div>
        </div>
      )}

      {/* Estado 3: Enlace no disponible / caducado o inválido */}
      {state === "expired" && (
        <div className="flex flex-col text-left">
          <div className="w-14 h-14 rounded-full bg-terracotta-light ring-8 ring-terracotta-light/40 flex items-center justify-center text-terracotta mb-5 shadow-xs border border-terracotta/20">
            <Link2Off className="w-6 h-6 text-terracotta" aria-hidden="true" />
          </div>

          <div className="inline-flex items-center gap-2 mb-2.5 px-3 py-1 rounded-full bg-terracotta-light/60 border border-terracotta/20 w-fit">
            <span className="w-1.5 h-1.5 rounded-full bg-terracotta" aria-hidden="true" />
            <span className="text-[0.6875rem] uppercase tracking-[0.2em] font-semibold text-terracotta font-sans">
              Enlace no disponible
            </span>
          </div>

          <h1 className="font-serif text-3xl md:text-[2.125rem] text-midnight tracking-tight font-normal mt-1 leading-tight">
            No pudimos verificar tu correo
          </h1>

          <p className="text-sm text-midnight-muted mt-3 leading-relaxed font-sans">
            El enlace de verificación es inválido o ha caducado. Para continuar, puedes intentar
            iniciar sesión o regresar a la portada.
          </p>

          <div className="mt-8 pt-6 border-t border-sage/30 flex flex-col gap-3 relative">
            <div className="absolute left-1/2 -top-2.5 -translate-x-1/2 bg-white px-2 text-sage/70 text-xs select-none" aria-hidden="true">
              •
            </div>

            <Link
              href="/auth/login"
              className="inline-flex items-center justify-center gap-2 w-full py-3.5 px-6 rounded-xl text-xs md:text-sm font-semibold uppercase tracking-[0.14em] bg-forest text-canvas hover:bg-forest-hover transition-colors shadow-sm select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
            >
              <span>Iniciar sesión</span>
            </Link>

            <Link
              href="/"
              className="inline-flex items-center justify-center gap-2 w-full py-3 px-6 rounded-xl text-xs md:text-sm font-semibold uppercase tracking-[0.14em] text-forest border border-sage/50 hover:bg-sage-subtle transition-colors select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
            >
              <span>Volver al inicio</span>
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}

export function EmailVerificationSkeleton() {
  return (
    <div className="flex flex-col text-left animate-pulse">
      <div className="inline-flex items-center gap-2 mb-2">
        <span className="w-2 h-2 rounded-full bg-sage-light" aria-hidden="true" />
        <div className="h-3 w-28 bg-sage-light rounded" />
      </div>
      <div className="h-8 w-3/4 bg-sage-light rounded mt-2" />
      <div className="space-y-2 mt-4">
        <div className="h-4 w-full bg-sage-light/60 rounded" />
        <div className="h-4 w-5/6 bg-sage-light/60 rounded" />
      </div>
      <div className="mt-6 pt-5 border-t border-sage/30 flex flex-col gap-3">
        <div className="h-12 w-full bg-sage-light rounded-xl" />
        <div className="h-4 w-1/3 bg-sage-light/60 rounded mx-auto" />
      </div>
    </div>
  );
}
