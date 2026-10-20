"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Home,
  SunMedium,
  ClipboardCheck,
  Compass,
  BookOpen,
  UserCircle,
  Menu,
  X,
  ChevronDown,
} from "lucide-react";

interface NavSubItem {
  name: string;
  href: string;
  description?: string;
}

interface NavItem {
  name: string;
  href: string;
  icon: typeof Home;
  subItems?: NavSubItem[];
}

const NAVIGATION_ITEMS: NavItem[] = [
  { name: "Inicio", href: "/inicio", icon: Home },
  { name: "Check-in", href: "/check-in", icon: SunMedium },
  { name: "Evaluaciones", href: "/evaluaciones", icon: ClipboardCheck },
  {
    name: "Mi recorrido",
    href: "/mi-recorrido",
    icon: Compass,
    subItems: [
      { name: "Historial (Registros)", href: "/mi-recorrido/historial/registros" },
      { name: "Historial (Evaluaciones)", href: "/mi-recorrido/historial/evaluaciones" },
      { name: "Tendencias", href: "/mi-recorrido/tendencias" },
    ],
  },
  { name: "Recursos", href: "/recursos", icon: BookOpen },
  { name: "Cuenta", href: "/cuenta", icon: UserCircle },
];

export function AppNav() {
  const pathname = usePathname();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [recorridoOpen, setRecorridoOpen] = useState(false);

  const isActive = (href: string) => {
    if (href === "/inicio") {
      return pathname === "/" || pathname === "/inicio";
    }
    return pathname.startsWith(href);
  };

  return (
    <nav className="flex items-center" aria-label="Navegación principal">
      {/* Navegación Desktop */}
      <div className="hidden lg:flex lg:items-center lg:gap-1">
        {NAVIGATION_ITEMS.map((item) => {
          const Icon = item.icon;
          const active = isActive(item.href);

          if (item.subItems) {
            return (
              <div
                key={item.name}
                className="relative"
                onMouseEnter={() => setRecorridoOpen(true)}
                onMouseLeave={() => setRecorridoOpen(false)}
              >
                <Link
                  href={item.href}
                  className={`inline-flex items-center gap-1.5 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                    active
                      ? "bg-sage-light text-forest"
                      : "text-midnight-muted hover:bg-sage/10 hover:text-midnight"
                  }`}
                  aria-expanded={recorridoOpen}
                >
                  <Icon className="h-4 w-4" aria-hidden="true" />
                  <span>{item.name}</span>
                  <ChevronDown className="h-3 w-3 opacity-60" aria-hidden="true" />
                </Link>

                {recorridoOpen && (
                  <div className="absolute left-0 top-full z-30 mt-1 w-56 rounded-lg border border-sage/40 bg-white p-1.5 shadow-md">
                    {item.subItems.map((sub) => {
                      const subActive = pathname === sub.href;
                      return (
                        <Link
                          key={sub.href}
                          href={sub.href}
                          className={`block rounded-md px-3 py-2 text-xs font-medium transition-colors ${
                            subActive
                              ? "bg-sage-light text-forest font-semibold"
                              : "text-midnight-muted hover:bg-sage/10 hover:text-midnight"
                          }`}
                        >
                          {sub.name}
                        </Link>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          }

          return (
            <Link
              key={item.name}
              href={item.href}
              className={`inline-flex items-center gap-1.5 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                active
                  ? "bg-sage-light text-forest font-semibold"
                  : "text-midnight-muted hover:bg-sage/10 hover:text-midnight"
              }`}
            >
              <Icon className="h-4 w-4" aria-hidden="true" />
              <span>{item.name}</span>
            </Link>
          );
        })}
      </div>

      {/* Botón menú móvil */}
      <div className="flex lg:hidden">
        <button
          type="button"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          className="inline-flex items-center justify-center rounded-md p-2 text-midnight-muted hover:bg-sage/15 hover:text-midnight focus:outline-none focus:ring-2 focus:ring-forest"
          aria-expanded={mobileMenuOpen}
          aria-controls="mobile-navigation"
          aria-label={mobileMenuOpen ? "Cerrar menú principal" : "Abrir menú principal"}
        >
          {mobileMenuOpen ? (
            <X className="h-6 w-6" aria-hidden="true" />
          ) : (
            <Menu className="h-6 w-6" aria-hidden="true" />
          )}
        </button>
      </div>

      {/* Drawer Móvil */}
      {mobileMenuOpen && (
        <div
          id="mobile-navigation"
          className="fixed inset-x-0 top-16 z-40 border-b border-sage/40 bg-canvas p-4 shadow-lg lg:hidden"
        >
          <div className="space-y-1">
            {NAVIGATION_ITEMS.map((item) => {
              const Icon = item.icon;
              const active = isActive(item.href);

              return (
                <div key={item.name} className="space-y-1">
                  <Link
                    href={item.href}
                    onClick={() => setMobileMenuOpen(false)}
                    className={`flex items-center gap-3 rounded-md px-3 py-2.5 text-base font-medium transition-colors ${
                      active
                        ? "bg-sage-light text-forest font-semibold"
                        : "text-midnight hover:bg-sage/10"
                    }`}
                  >
                    <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
                    <span>{item.name}</span>
                  </Link>

                  {item.subItems && (
                    <div className="ml-8 space-y-1 border-l-2 border-sage/40 pl-3">
                      {item.subItems.map((sub) => {
                        const subActive = pathname === sub.href;
                        return (
                          <Link
                            key={sub.href}
                            href={sub.href}
                            onClick={() => setMobileMenuOpen(false)}
                            className={`block rounded-md px-2 py-1.5 text-sm transition-colors ${
                              subActive
                                ? "text-forest font-semibold"
                                : "text-midnight-muted hover:text-midnight"
                            }`}
                          >
                            {sub.name}
                          </Link>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </nav>
  );
}
