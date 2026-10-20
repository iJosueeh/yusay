import Link from "next/link";
import { SunMedium, ClipboardCheck, Compass, BookOpen, ArrowRight } from "lucide-react";

export const metadata = {
  title: "Inicio",
};

export default function InicioPage() {
  return (
    <div className="space-y-10">
      {/* Encabezado Editorial */}
      <section className="space-y-3">
        <h1 className="text-3xl font-serif font-bold text-forest sm:text-4xl">
          Comprende tu bienestar a través del tiempo
        </h1>
        <p className="max-w-2xl text-base text-midnight-muted sm:text-lg">
          Un espacio sereno y confidencial para explorar tus dinámicas emocionales,
          respaldado por escalas validadas y privacidad por diseño.
        </p>
      </section>

      {/* Cuadrícula de Módulos */}
      <section className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {/* Check-in */}
        <div className="flex flex-col justify-between rounded-xl border border-sage/50 bg-white p-6 shadow-xs transition-shadow hover:shadow-sm">
          <div className="space-y-3">
            <div className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-sage-light text-forest">
              <SunMedium className="h-5 w-5" aria-hidden="true" />
            </div>
            <h2 className="text-lg font-serif font-semibold text-midnight">Check-in Diario</h2>
            <p className="text-xs text-midnight-muted leading-relaxed">
              Registra en menos de un minuto tu percepción sobre ánimo, estrés, energía y sueño en escala 1 a 5.
            </p>
          </div>
          <div className="pt-4">
            <Link
              href="/check-in"
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-forest hover:text-forest-hover"
            >
              <span>Comenzar registro</span>
              <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
            </Link>
          </div>
        </div>

        {/* Evaluaciones */}
        <div className="flex flex-col justify-between rounded-xl border border-sage/50 bg-white p-6 shadow-xs transition-shadow hover:shadow-sm">
          <div className="space-y-3">
            <div className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-sage-light text-forest">
              <ClipboardCheck className="h-5 w-5" aria-hidden="true" />
            </div>
            <h2 className="text-lg font-serif font-semibold text-midnight">Evaluaciones</h2>
            <p className="text-xs text-midnight-muted leading-relaxed">
              Instrumentos psicométricos estructurados con cálculo oficial y orientación psicoeducativa personalizada.
            </p>
          </div>
          <div className="pt-4">
            <Link
              href="/evaluaciones"
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-forest hover:text-forest-hover"
            >
              <span>Explorar catálogo</span>
              <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
            </Link>
          </div>
        </div>

        {/* Mi Recorrido */}
        <div className="flex flex-col justify-between rounded-xl border border-sage/50 bg-white p-6 shadow-xs transition-shadow hover:shadow-sm">
          <div className="space-y-3">
            <div className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-sage-light text-forest">
              <Compass className="h-5 w-5" aria-hidden="true" />
            </div>
            <h2 className="text-lg font-serif font-semibold text-midnight">Mi Recorrido</h2>
            <p className="text-xs text-midnight-muted leading-relaxed">
              Consulta el historial cronológico de tus registros y visualiza la evolución de tus tendencias longitudinales.
            </p>
          </div>
          <div className="pt-4">
            <Link
              href="/mi-recorrido/tendencias"
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-forest hover:text-forest-hover"
            >
              <span>Ver tendencias</span>
              <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
            </Link>
          </div>
        </div>

        {/* Recursos */}
        <div className="flex flex-col justify-between rounded-xl border border-sage/50 bg-white p-6 shadow-xs transition-shadow hover:shadow-sm">
          <div className="space-y-3">
            <div className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-sage-light text-forest">
              <BookOpen className="h-5 w-5" aria-hidden="true" />
            </div>
            <h2 className="text-lg font-serif font-semibold text-midnight">Recursos</h2>
            <p className="text-xs text-midnight-muted leading-relaxed">
              Artículos y enlaces psicoeducativos curados para profundizar en herramientas de autocuidado.
            </p>
          </div>
          <div className="pt-4">
            <Link
              href="/recursos"
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-forest hover:text-forest-hover"
            >
              <span>Explorar recursos</span>
              <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
            </Link>
          </div>
        </div>
      </section>

      {/* Nota de Estado Técnico */}
      <section className="rounded-xl border border-sage/30 bg-sage-subtle/30 p-6">
        <h3 className="text-sm font-semibold text-forest">Base técnica inicializada</h3>
        <p className="mt-1 text-xs text-midnight-muted leading-relaxed">
          Estructura base, sistema de tokens (#F7F5F0, #304D43, #A6B49B, #C98268, #273340), tipografías Newsreader y DM Sans,
          cliente HTTP server-only y layout compartido listos para la implementación modular por fases.
        </p>
      </section>
    </div>
  );
}
