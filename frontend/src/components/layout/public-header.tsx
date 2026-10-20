import Link from "next/link";
import { PublicMobileNav } from "./public-mobile-nav";
import { Button } from "@/components/ui/button";

export function PublicHeader() {
  return (
    <header className="fixed top-0 left-0 right-0 z-50 bg-canvas/92 backdrop-blur-md border-b border-sage/40 transition-all">
      <div className="h-20 max-w-7xl mx-auto px-6 lg:px-12 flex items-center justify-between gap-6 relative">
        {/* Logotipo */}
        <Link
          href="/"
          className="flex items-center gap-2 group transition-transform duration-200 hover:-translate-y-0.5"
          aria-label="Yusay — Ir al inicio"
        >
          <span className="font-serif text-2xl md:text-3xl tracking-tight text-forest font-normal group-hover:text-forest-hover transition-colors">
            Yusay
          </span>
          <span
            className="w-1.5 h-1.5 rounded-full bg-terracotta mb-1"
            aria-hidden="true"
          />
        </Link>

        {/* Navegación Desktop */}
        <nav
          aria-label="Navegación principal pública"
          className="hidden md:flex items-center gap-8 text-[0.875rem] font-sans font-medium tracking-wide text-midnight-muted"
        >
          <a
            href="#concepto"
            className="hover:text-forest hover:-translate-y-0.5 transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/40 rounded-sm px-1"
          >
            Concepto
          </a>
          <a
            href="#metodo"
            className="hover:text-forest hover:-translate-y-0.5 transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/40 rounded-sm px-1"
          >
            Cómo funciona
          </a>
          <a
            href="#herramientas"
            className="hover:text-forest hover:-translate-y-0.5 transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/40 rounded-sm px-1"
          >
            Herramientas
          </a>
          <a
            href="#privacidad"
            className="hover:text-forest hover:-translate-y-0.5 transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/40 rounded-sm px-1"
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
