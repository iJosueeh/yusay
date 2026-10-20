import { AppHeader } from "./app-header";
import { DisclaimerBanner } from "./disclaimer-banner";
import { AppFooter } from "./app-footer";

interface PrivateLayoutProps {
  children: React.ReactNode;
}

/**
 * Layout reutilizable para pantallas privadas de Yusay.
 * Componente de Servidor (RSC) que encapsula la cabecera, navegación responsive,
 * banner normativo no clínico y pie editorial.
 */
export function PrivateLayout({ children }: PrivateLayoutProps) {
  return (
    <div className="flex min-h-screen flex-col bg-canvas text-midnight selection:bg-sage/30 selection:text-forest">
      <AppHeader />
      <DisclaimerBanner />
      <main className="flex-1">
        <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
          {children}
        </div>
      </main>
      <AppFooter />
    </div>
  );
}
