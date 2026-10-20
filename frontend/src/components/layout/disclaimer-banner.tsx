import { ShieldAlert } from "lucide-react";

/**
 * Banner normativo del alcance no clínico de Yusay.
 * Componente de servidor (RSC) reutilizable en layouts de la aplicación.
 */
export function DisclaimerBanner() {
  return (
    <aside
      role="note"
      aria-label="Aviso sobre alcance del servicio"
      className="border-t border-b border-sage/40 bg-sage-subtle/50 px-4 py-2.5 sm:px-6"
    >
      <div className="mx-auto flex max-w-7xl items-center justify-between gap-3 text-xs text-midnight-muted">
        <div className="flex items-center gap-2">
          <ShieldAlert className="h-4 w-4 shrink-0 text-terracotta" aria-hidden="true" />
          
          <p>
            <strong className="font-medium text-midnight">Aviso importante:</strong> Yusay es una herramienta de exploración y seguimiento personal del bienestar emocional. No constituye un diagnóstico médico ni reemplaza la atención psicológica profesional.
          </p>
        </div>
      </div>
    </aside>
  );
}
