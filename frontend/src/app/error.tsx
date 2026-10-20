"use client";

import { useEffect } from "react";
import Link from "next/link";
import { AlertCircle, RotateCcw, Home } from "lucide-react";

interface ErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function ErrorBoundary({ error, reset }: ErrorProps) {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production") {
      console.error("ErrorBoundary capturó:", error);
    }
  }, [error]);

  return (
    <div className="min-h-screen bg-canvas text-midnight flex flex-col justify-between p-6 sm:p-12">
      <header className="max-w-4xl w-full mx-auto flex items-center justify-between">
        <Link
          href="/"
          className="flex items-center gap-2 group"
          aria-label="Yusay — Ir al inicio"
        >
          <span className="font-serif text-2xl tracking-tight text-forest font-normal group-hover:text-forest-hover transition-colors">
            Yusay
          </span>
          <span
            className="w-1.5 h-1.5 rounded-full bg-terracotta mb-0.5"
            aria-hidden="true"
          />
        </Link>
      </header>

      <main className="max-w-lg w-full mx-auto my-auto py-12 text-center space-y-6">
        <div className="w-14 h-14 rounded-full bg-terracotta-light mx-auto flex items-center justify-center text-terracotta">
          <AlertCircle className="h-6 w-6" aria-hidden="true" />
        </div>

        <div className="space-y-3">
          <span className="text-xs uppercase tracking-widest text-terracotta font-semibold font-sans">
            Inconveniente temporal
          </span>
          <h1 className="font-serif text-3xl sm:text-4xl text-forest font-normal">
            Ha ocurrido una pausa inesperada
          </h1>
          <p className="font-sans text-sm sm:text-base text-midnight-muted leading-relaxed">
            No fue posible cargar esta sección en este instante. Tu información y
            registros personales permanecen seguros y no se han visto afectados.
          </p>
        </div>

        <div className="pt-2 flex flex-col sm:flex-row items-center justify-center gap-3">
          <button
            type="button"
            onClick={reset}
            className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-full bg-forest text-canvas font-sans text-xs uppercase tracking-wider font-semibold hover:bg-forest-hover transition-colors shadow-sm w-full sm:w-auto"
          >
            <RotateCcw className="h-4 w-4" aria-hidden="true" />
            <span>Intentar nuevamente</span>
          </button>

          <Link
            href="/"
            className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-full border border-forest/30 text-forest font-sans text-xs uppercase tracking-wider font-semibold hover:bg-forest/5 transition-colors w-full sm:w-auto"
          >
            <Home className="h-4 w-4" aria-hidden="true" />
            <span>Volver a la portada</span>
          </Link>
        </div>
      </main>

      <footer className="max-w-4xl w-full mx-auto text-center text-xs text-midnight-muted/80">
        <p>Yusay — Seguimiento personal del bienestar emocional (no clínico).</p>
      </footer>
    </div>
  );
}
