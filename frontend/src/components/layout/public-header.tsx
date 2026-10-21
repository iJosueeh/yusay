import Link from "next/link";
import { PublicMobileNav } from "./public-mobile-nav";
import { Button } from "@/components/ui/button";

export function PublicHeader() {
  return (
    <header className="fixed top-0 left-0 right-0 z-50 bg-snow-white/95 backdrop-blur-md border-b border-warm-stone transition-colors">
      {/* Top Announcement Banner (40px) */}
      <div className="h-10 bg-warm-stone border-b border-frosted-glass/40 px-4 sm:px-6 flex items-center justify-center text-[11px] sm:text-xs font-mono uppercase tracking-wider text-forest-depths select-none">
        <span className="inline-flex items-center gap-2">
          <span className="w-1.5 h-1.5 rounded-full bg-lime-pulse shrink-0" aria-hidden="true" />
          <span>Privacidad total // Sin pedir datos personales ni permanencia</span>
          <span className="hidden sm:inline text-pewter">·</span>
          <a
            href="#privacidad"
            className="hidden sm:inline-flex items-center gap-1 border-b border-forest-depths font-medium hover:opacity-75 transition-opacity"
          >
            <span>Conocer el compromiso</span>
            <span>→</span>
          </a>
        </span>
      </div>

      <div className="h-20 max-w-7xl mx-auto px-6 lg:px-12 flex items-center justify-between gap-6 relative">
        {/* Logotipo */}
        <Link
          href="/"
          className="flex items-center gap-2 group transition-opacity hover:opacity-85"
          aria-label="Yusay — Ir al inicio"
        >
          <span className="font-serif text-2xl md:text-3xl tracking-tight text-forest-depths font-light transition-colors">
            Yusay
          </span>
          <span
            className="w-1.5 h-1.5 rounded-full bg-forest-depths mb-1"
            aria-hidden="true"
          />
        </Link>

        {/* Navegación Desktop */}
        <nav
          aria-label="Navegación principal pública"
          className="hidden md:flex items-center gap-8 text-[0.875rem] font-sans font-normal tracking-normal text-pewter"
        >
          <a
            href="#concepto"
            className="hover:text-forest-depths transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/40 rounded-sm px-1"
          >
            Concepto
          </a>
          <a
            href="#metodo"
            className="hover:text-forest-depths transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/40 rounded-sm px-1"
          >
            Cómo funciona
          </a>
          <a
            href="#herramientas"
            className="hover:text-forest-depths transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/40 rounded-sm px-1"
          >
            Herramientas
          </a>
          <a
            href="#privacidad"
            className="hover:text-forest-depths transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/40 rounded-sm px-1"
          >
            Privacidad
          </a>
        </nav>

        {/* Acciones de acceso / registro */}
        <div className="flex items-center gap-3">
          <Button
            href="/auth/login"
            variant="ghost"
            size="sm"
            className="hidden sm:inline-flex"
          >
            Iniciar sesión
          </Button>

          <Button
            href="/auth/registro"
            variant="primary"
            size="sm"
            className="hidden sm:inline-flex"
          >
            Comenzar
          </Button>

          <PublicMobileNav />
        </div>
      </div>
    </header>
  );
}
