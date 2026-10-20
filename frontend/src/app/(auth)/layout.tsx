import Link from "next/link";

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-screen bg-canvas flex flex-col justify-between p-6 sm:p-10 text-midnight">
      {/* Encabezado mínimo */}
      <header className="max-w-md w-full mx-auto flex items-center justify-between">
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

        <Link
          href="/"
          className="text-xs font-sans text-midnight-muted hover:text-forest transition-colors"
        >
          ← Volver al inicio
        </Link>
      </header>

      {/* Contenedor central */}
      <main className="max-w-md w-full mx-auto my-auto py-10">
        {children}
      </main>

      {/* Pie mínimo de aviso */}
      <footer className="max-w-md w-full mx-auto text-center text-xs text-midnight-muted/80">
        <p>Yusay — Plataforma de seguimiento personal del bienestar (no clínica).</p>
      </footer>
    </div>
  );
}
