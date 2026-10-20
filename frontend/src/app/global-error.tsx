"use client";

import { useEffect } from "react";
import { AlertCircle, RotateCcw, RefreshCw } from "lucide-react";

interface GlobalErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function GlobalError({ error, reset }: GlobalErrorProps) {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production") {
      console.error("GlobalError capturó:", error);
    }
  }, [error]);

  const handleReload = () => {
    if (typeof window !== "undefined") {
      window.location.reload();
    }
  };

  return (
    <html lang="es">
      <body className="min-h-screen bg-[#F7F5F0] text-[#273340] font-sans flex flex-col justify-between p-6 sm:p-12 antialiased">
        <header className="max-w-4xl w-full mx-auto flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="font-serif text-2xl tracking-tight text-[#304D43] font-normal">
              Yusay
            </span>
            <span
              className="w-1.5 h-1.5 rounded-full bg-[#C98268] mb-0.5"
              aria-hidden="true"
            />
          </div>
        </header>

        <main className="max-w-md w-full mx-auto my-auto py-12 text-center space-y-6">
          <div className="w-14 h-14 rounded-full bg-[#faece6] mx-auto flex items-center justify-center text-[#C98268]">
            <AlertCircle className="h-6 w-6" aria-hidden="true" />
          </div>

          <div className="space-y-3">
            <span className="text-xs uppercase tracking-widest text-[#C98268] font-semibold">
              Error Global
            </span>
            <h1 className="font-serif text-3xl sm:text-4xl text-[#304D43] font-normal">
              Algo interrumpió la conexión
            </h1>
            <p className="text-sm text-[#56687a] leading-relaxed">
              Ocurrió un error en la carga de la aplicación. Tu información
              permanece intacta y segura.
            </p>
          </div>

          <div className="pt-2 flex flex-col sm:flex-row items-center justify-center gap-3">
            <button
              type="button"
              onClick={reset}
              className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-full bg-[#304D43] text-[#F7F5F0] text-xs uppercase tracking-wider font-semibold hover:bg-[#263e36] transition-colors shadow-sm w-full sm:w-auto"
            >
              <RotateCcw className="h-4 w-4" aria-hidden="true" />
              <span>Restablecer aplicación</span>
            </button>

            <button
              type="button"
              onClick={handleReload}
              className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-full border border-[#304D43]/30 text-[#304D43] text-xs uppercase tracking-wider font-semibold hover:bg-[#304D43]/5 transition-colors w-full sm:w-auto"
            >
              <RefreshCw className="h-4 w-4" aria-hidden="true" />
              <span>Recargar ventana</span>
            </button>
          </div>
        </main>

        <footer className="max-w-4xl w-full mx-auto text-center text-xs text-[#56687a]">
          <p>Yusay — Seguimiento personal del bienestar emocional (no clínico).</p>
        </footer>
      </body>
    </html>
  );
}
