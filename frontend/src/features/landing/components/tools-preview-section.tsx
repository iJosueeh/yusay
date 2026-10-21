import Image from "next/image";
import toolsImg1 from "@/assets/tools-img-1.png";
import { Reveal } from "@/components/ui/reveal";
import { InteractiveCheckInMockup } from "./interactive-checkin-preview";

export function ToolsPreviewSection() {
  return (
    <section
      id="herramientas"
      className="bg-forest-depths text-snow-white border-y border-forest-depths py-20 lg:py-28"
    >
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        {/* Título de Sección con redacción optimizada para Check-in */}
        <Reveal
          duration={0.5}
          yOffset={16}
          className="text-center max-w-2xl mx-auto mb-12 space-y-2.5"
        >
          <span className="text-xs font-mono uppercase tracking-widest text-lime-pulse font-medium">
            Herramientas cotidianas
          </span>
          <h2 className="font-serif text-3xl sm:text-4xl text-snow-white font-light tracking-tight">
            Una pausa breve para observar tu recorrido
          </h2>
          <p className="font-sans text-sm sm:text-base text-snow-white/80">
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
            <p className="text-xs text-snow-white/60 text-center font-mono">
              * Demostración interactiva de la pausa diaria en Yusay con los cuatro aspectos esenciales.
            </p>
          </Reveal>

          {/* Columna Derecha: Fotografía Workspace + Resumen de Tendencias (6 cols) */}
          <Reveal
            delay={0.2}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-6 space-y-4"
          >
            {/* Fotografía de escritorio sereno */}
            <div className="group rounded-2xl overflow-hidden shadow-none border border-snow-white/15 max-h-[290px] sm:max-h-[310px] aspect-[16/9] bg-forest-depths">
              <Image
                src={toolsImg1}
                alt="Escritorio sereno con taza de infusión, cuaderno abierto y luz matinal"
                className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-700 ease-out motion-reduce:transform-none motion-reduce:transition-none"
              />
            </div>

            {/* Tarjeta de Tendencias (16px, shadowless en Dark Section) */}
            <div className="bg-snow-white/5 border border-snow-white/15 rounded-2xl p-6 sm:p-7 shadow-none">
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2.5">
                  <span className="px-2 py-0.5 rounded-full border border-snow-white/30 font-mono text-[10px] font-medium text-snow-white uppercase tracking-wider">
                    TR-03
                  </span>
                  <div>
                    <span className="text-[0.6875rem] uppercase tracking-wider text-snow-white/70 font-mono block">
                      Evolución Temporal
                    </span>
                    <h3 className="font-serif text-lg text-snow-white font-light tracking-tight">
                      Historial de dimensiones
                    </h3>
                  </div>
                </div>
                <span className="text-xs text-snow-white font-mono bg-snow-white/10 px-3 py-1 rounded-full border border-snow-white/20">
                  Últimos 14 días
                </span>
              </div>

              <p className="text-xs text-snow-white/70 mb-4 font-sans">
                Comportamiento de tus dimensiones registradas a lo largo de las semanas:
              </p>

              <div className="space-y-2.5 font-sans">
                <div className="p-3 rounded-lg bg-snow-white/5 border border-snow-white/10 flex items-center justify-between text-xs">
                  <span className="text-snow-white font-medium">
                    Ánimo general
                  </span>
                  <span className="text-lime-pulse font-mono text-[11px] font-medium">
                    Mediana 4 · Predominio sereno
                  </span>
                </div>

                <div className="p-3 rounded-lg bg-snow-white/5 border border-snow-white/10 flex items-center justify-between text-xs">
                  <span className="text-snow-white font-medium">
                    Nivel de Estrés
                  </span>
                  <span className="text-snow-white/70 font-mono text-[11px]">
                    Mediana 2 · Variabilidad baja
                  </span>
                </div>

                <div className="p-3 rounded-lg bg-snow-white/5 border border-snow-white/10 flex items-center justify-between text-xs">
                  <span className="text-snow-white font-medium">
                    Nivel de Energía
                  </span>
                  <span className="text-snow-white/90 font-mono text-[11px] font-medium">
                    Mediana 3 · Constante
                  </span>
                </div>

                <div className="p-3 rounded-lg bg-snow-white/5 border border-snow-white/10 flex items-center justify-between text-xs">
                  <span className="text-snow-white font-medium">
                    Calidad de Sueño
                  </span>
                  <span className="text-lime-pulse font-mono text-[11px] font-medium">
                    Mediana 4 · Reposo satisfactorio
                  </span>
                </div>
              </div>

              <div className="pt-3 border-t border-snow-white/10 mt-4 flex items-center justify-between">
                <span className="text-[11px] font-mono uppercase text-snow-white/60">
                  Medianas ordinales sin sesgo
                </span>
                <a
                  href="/auth/registro"
                  className="inline-flex items-center gap-1.5 text-xs font-mono uppercase tracking-wider text-snow-white border-b-[1.5px] border-snow-white pb-0.5 hover:opacity-75 transition-opacity"
                >
                  <span>Ver registro completo</span>
                  <span>→</span>
                </a>
              </div>
            </div>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
