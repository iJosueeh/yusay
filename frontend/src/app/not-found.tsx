import Link from "next/link";
import { Compass, ArrowLeft } from "lucide-react";

export const metadata = {
  title: "Página no encontrada",
};

export default function NotFound() {
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
        <div className="w-14 h-14 rounded-full bg-sage-light mx-auto flex items-center justify-center text-forest">
          <Compass className="h-6 w-6 text-forest" aria-hidden="true" />
        </div>

        <div className="space-y-3">
          <span className="text-xs uppercase tracking-widest text-terracotta font-semibold font-sans">
            Error 404
          </span>
          <h1 className="font-serif text-3xl sm:text-4xl text-forest font-normal">
            Este rincón no está disponible
          </h1>
          <p className="font-sans text-sm sm:text-base text-midnight-muted leading-relaxed">
            La página que buscas no existe, ha sido movida o la dirección fue
            escrita incorrectamente.
          </p>
        </div>

        <div className="pt-2 flex flex-col sm:flex-row items-center justify-center gap-3">
          <Link
            href="/"
            className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-full bg-forest text-canvas font-sans text-xs uppercase tracking-wider font-semibold hover:bg-forest-hover transition-colors shadow-sm w-full sm:w-auto"
          >
            <ArrowLeft className="h-4 w-4" aria-hidden="true" />
            <span>Volver a la página principal</span>
          </Link>

          <Link
            href="/inicio"
            className="inline-flex items-center justify-center px-6 py-3 rounded-full border border-forest/30 text-forest font-sans text-xs uppercase tracking-wider font-semibold hover:bg-forest/5 transition-colors w-full sm:w-auto"
          >
            Ir a mi espacio
          </Link>
        </div>
      </main>

      <footer className="max-w-4xl w-full mx-auto text-center text-xs text-midnight-muted/80">
        <p>Yusay — Seguimiento personal del bienestar emocional (no clínico).</p>
      </footer>
    </div>
  );
}
