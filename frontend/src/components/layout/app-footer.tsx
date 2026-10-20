import Link from "next/link";

export function AppFooter() {
  return (
    <footer className="border-t border-sage/40 bg-canvas py-10 text-midnight-muted">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col items-center justify-between gap-6 sm:flex-row">
          <div className="flex items-center gap-2">
            <span className="text-lg font-serif font-semibold text-forest">Yusay</span>
            <span className="text-xs text-midnight-subtle">— Bienestar a través del tiempo</span>
          </div>

          <div className="flex flex-wrap items-center justify-center gap-6 text-xs font-medium">
            <Link
              href="/aviso-legal"
              className="transition-colors hover:text-midnight focus:underline"
            >
              Aviso Legal y Alcance
            </Link>
            <Link
              href="/privacidad"
              className="transition-colors hover:text-midnight focus:underline"
            >
              Política de Privacidad
            </Link>
            <span className="text-midnight-subtle">
              © 2026 Yusay. Todos los derechos reservados.
            </span>
          </div>
        </div>
      </div>
    </footer>
  );
}
