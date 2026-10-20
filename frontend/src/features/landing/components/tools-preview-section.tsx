import Image from "next/image";
import toolsImg1 from "@/assets/tools-img-1.png";
import { Reveal } from "@/components/ui/reveal";
import { InteractiveCheckInMockup } from "./interactive-checkin-preview";

export function ToolsPreviewSection() {
  return (
    <section
      id="herramientas"
      className="bg-sage-subtle/50 border-y border-sage/40 py-14 lg:py-20"
    >
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        {/* Título de Sección con redacción optimizada para Check-in */}
        <Reveal
          duration={0.5}
          yOffset={16}
          className="text-center max-w-2xl mx-auto mb-12 space-y-2.5"
        >
          <span className="text-xs font-semibold uppercase tracking-widest text-terracotta">
            Herramientas cotidianas
          </span>
          <h2 className="font-serif text-3xl sm:text-4xl text-forest font-normal">
            Una pausa breve para observar tu recorrido
          </h2>
          <p className="font-sans text-sm sm:text-base text-midnight-muted">
            Diseñado para registrar tu estado en menos de un minuto, sin fricciones,
            fórmulas complejas ni exigencias de productividad.
          </p>
        </Reveal>

        {/* Composición Mixta: Mockup Interactivo + Fotografía y Tendencias */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
          {/* Columna Izquierda: Check-in Interactivo (6 cols) */}
          <Reveal
            delay={0.1}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-6 space-y-3"
          >
            <InteractiveCheckInMockup />
            <p className="text-xs text-midnight-muted/80 text-center font-sans">
              * Demostración interactiva del check-in diario en Yusay con las cuatro dimensiones oficiales.
            </p>
          </Reveal>

          {/* Columna Derecha: Fotografía Workspace + Resumen de Tendencias (6 cols) */}
          <Reveal
            delay={0.2}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-6 space-y-4"
          >
            {/* Fotografía de escritorio sereno con hover mejorado */}
            <div className="group rounded-2xl overflow-hidden shadow-sm border border-sage/40 max-h-[290px] sm:max-h-[310px] aspect-[16/9] bg-white transition-all duration-300 hover:shadow-md hover:border-forest/40">
              <Image
                src={toolsImg1}
                alt="Escritorio sereno con taza de infusión, cuaderno abierto y luz matinal"
                className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-700 ease-out motion-reduce:transform-none motion-reduce:transition-none"
              />
            </div>

            {/* Tarjeta de Tendencias con microinteracciones y hovers */}
            <div className="bg-white border border-sage/40 rounded-2xl p-6 sm:p-7 shadow-sm transition-all duration-300 hover:border-forest/50 hover:shadow-md hover:-translate-y-0.5 motion-reduce:hover:transform-none">
              <div className="flex items-center justify-between mb-3">
                <div>
                  <span className="text-[0.6875rem] uppercase tracking-wider text-forest font-semibold block">
                    Evolución Temporal
                  </span>
                  <h3 className="font-serif text-lg text-forest font-normal">
                    Historial de dimensiones
                  </h3>
                </div>
                <span className="text-xs text-forest font-medium bg-sage-light px-3 py-1 rounded-full border border-sage/30 transition-colors hover:bg-sage/40">
                  Últimos 14 días
                </span>
              </div>

              <p className="text-xs text-midnight-muted mb-4">
                Comportamiento de tus dimensiones registradas a lo largo de las semanas:
              </p>

              <div className="space-y-2.5">
                <div className="p-3 rounded-xl bg-canvas border border-sage/20 flex items-center justify-between text-xs transition-colors hover:bg-sage-subtle hover:border-forest/20">
                  <span className="text-midnight font-medium">
                    Ánimo general
                  </span>
                  <span className="text-forest font-semibold">
                    Mediana 4 · Predominio sereno
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-canvas border border-sage/20 flex items-center justify-between text-xs transition-colors hover:bg-sage-subtle hover:border-forest/20">
                  <span className="text-midnight font-medium">
                    Nivel de Estrés
                  </span>
                  <span className="text-midnight-muted font-medium">
                    Mediana 2 · Variabilidad baja
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-canvas border border-sage/20 flex items-center justify-between text-xs transition-colors hover:bg-sage-subtle hover:border-forest/20">
                  <span className="text-midnight font-medium">
                    Nivel de Energía
                  </span>
                  <span className="text-forest font-semibold">
                    Mediana 3 · Constante
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-canvas border border-sage/20 flex items-center justify-between text-xs transition-colors hover:bg-sage-subtle hover:border-forest/20">
                  <span className="text-midnight font-medium">
                    Calidad de Sueño
                  </span>
                  <span className="text-terracotta font-semibold">
                    Mediana 4 · Reposo satisfactorio
                  </span>
                </div>
              </div>
            </div>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
