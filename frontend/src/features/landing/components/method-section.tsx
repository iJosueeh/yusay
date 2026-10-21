import { PenLine, TrendingUp, BookOpen } from "lucide-react";
import { Reveal } from "@/components/ui/reveal";

export function MethodSection() {
  return (
    <section
      id="metodo"
      className="py-16 lg:py-24 max-w-7xl mx-auto px-6 lg:px-12 w-full"
    >
      {/* Cabecera de sección */}
      <Reveal
        duration={0.5}
        yOffset={16}
        className="flex flex-col md:flex-row md:items-end justify-between gap-4 mb-12"
      >
        <div className="space-y-2 max-w-xl">
          <span className="text-xs font-mono uppercase tracking-widest text-pewter">
            Método de uso
          </span>
          <h2 className="font-serif text-3xl sm:text-4xl text-forest-depths font-light tracking-tight">
            Un proceso sencillo integrado en tu día a día
          </h2>
        </div>
        <p className="text-sm text-pewter max-w-sm">
          Tres momentos complementarios pensados para encajar con naturalidad en tu
          rutina, sin exigir esfuerzo excesivo ni generar ansiedad.
        </p>
      </Reveal>

      {/* Tarjetas de las tres etapas con especímenes botánicos (16px, shadowless) */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 lg:gap-8">
        {/* 01 Registrar */}
        <Reveal
          delay={0.1}
          duration={0.5}
          yOffset={16}
          className="flex flex-col h-full"
        >
          <div className="bg-snow-white border border-warm-stone p-7 sm:p-8 rounded-2xl shadow-none flex flex-col justify-between h-full transition-colors hover:border-frosted-glass group">
            <div className="space-y-3.5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <span className="font-serif italic text-3xl text-forest-depths/60">
                    01
                  </span>
                  <span className="px-2 py-0.5 rounded-full border border-forest-depths/30 font-mono text-[10px] font-medium text-forest-depths uppercase tracking-wider">
                    RD-01
                  </span>
                </div>
                <span className="text-[0.6875rem] uppercase tracking-wider text-forest-depths bg-warm-stone px-2.5 py-1 rounded-full font-mono font-medium border border-frosted-glass/50">
                  Pausa de 1 minuto
                </span>
              </div>
              <h3 className="font-serif text-2xl text-forest-depths font-light tracking-tight">
                Pausa diaria consciente
              </h3>
              <p className="font-sans text-sm text-pewter leading-relaxed">
                Dedica un instante al final de la jornada para notar cómo te sientes
                en cuatro aspectos esenciales: ánimo, estrés, energía y descanso. Escala
                del 1 al 5 simple e intuitiva, con espacio libre para notas personales.
              </p>
            </div>
            <div className="pt-5 border-t border-warm-stone mt-6 flex items-center justify-between">
              <div className="flex items-center gap-2 text-xs uppercase tracking-wider text-forest-depths font-medium font-mono">
                <span className="p-1.5 rounded-full bg-warm-stone">
                  <PenLine className="h-3.5 w-3.5 text-forest-depths" />
                </span>
                <span>Registro diario</span>
              </div>
              <a
                href="#herramientas"
                className="inline-flex items-center gap-1 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity"
              >
                <span>Probar simulador</span>
                <span>→</span>
              </a>
            </div>
          </div>
        </Reveal>

        {/* 02 Observar */}
        <Reveal
          delay={0.2}
          duration={0.5}
          yOffset={16}
          className="flex flex-col h-full"
        >
          <div className="bg-snow-white border border-warm-stone p-7 sm:p-8 rounded-2xl shadow-none flex flex-col justify-between h-full transition-colors hover:border-frosted-glass group">
            <div className="space-y-3.5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <span className="font-serif italic text-3xl text-forest-depths/60">
                    02
                  </span>
                  <span className="px-2 py-0.5 rounded-full border border-forest-depths/30 font-mono text-[10px] font-medium text-forest-depths uppercase tracking-wider">
                    TR-03
                  </span>
                </div>
                <span className="text-[0.6875rem] uppercase tracking-wider text-forest-depths bg-warm-stone px-2.5 py-1 rounded-full font-mono font-medium border border-frosted-glass/50">
                  Seguimiento Visual
                </span>
              </div>
              <h3 className="font-serif text-2xl text-forest-depths font-light tracking-tight">
                Observar tendencias
              </h3>
              <p className="font-sans text-sm text-pewter leading-relaxed">
                Visualiza la evolución temporal de tus registros y cuestionarios a lo
                largo de las semanas. Patrones continuos presentados con claridad para
                comprender tus ritmos, sin juicios de rendimiento.
              </p>
            </div>
            <div className="pt-5 border-t border-warm-stone mt-6 flex items-center justify-between">
              <div className="flex items-center gap-2 text-xs uppercase tracking-wider text-forest-depths font-medium font-mono">
                <span className="p-1.5 rounded-full bg-warm-stone">
                  <TrendingUp className="h-3.5 w-3.5 text-forest-depths" />
                </span>
                <span>Historial y tendencias</span>
              </div>
              <a
                href="#herramientas"
                className="inline-flex items-center gap-1 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity"
              >
                <span>Ver historial</span>
                <span>→</span>
              </a>
            </div>
          </div>
        </Reveal>

        {/* 03 Comprender */}
        <Reveal
          delay={0.3}
          duration={0.5}
          yOffset={16}
          className="flex flex-col h-full"
        >
          <div className="bg-snow-white border border-warm-stone p-7 sm:p-8 rounded-2xl shadow-none flex flex-col justify-between h-full transition-colors hover:border-frosted-glass group">
            <div className="space-y-3.5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <span className="font-serif italic text-3xl text-forest-depths/60">
                    03
                  </span>
                  <span className="px-2 py-0.5 rounded-full border border-forest-depths/30 font-mono text-[10px] font-medium text-forest-depths uppercase tracking-wider">
                    RC-04
                  </span>
                </div>
                <span className="text-[0.6875rem] uppercase tracking-wider text-forest-depths bg-warm-stone px-2.5 py-1 rounded-full font-mono font-medium border border-frosted-glass/50">
                  Orientación Guiada
                </span>
              </div>
              <h3 className="font-serif text-2xl text-forest-depths font-light tracking-tight">
                Comprender y cuidar
              </h3>
              <p className="font-sans text-sm text-pewter leading-relaxed">
                Accede a una biblioteca de materiales psicoeducativos curados para
                profundizar en descanso, gestión del estrés y autocuidado con
                fundamento contrastado y lenguaje cercano.
              </p>
            </div>
            <div className="pt-5 border-t border-warm-stone mt-6 flex items-center justify-between">
              <div className="flex items-center gap-2 text-xs uppercase tracking-wider text-forest-depths font-medium font-mono">
                <span className="p-1.5 rounded-full bg-warm-stone">
                  <BookOpen className="h-3.5 w-3.5 text-forest-depths" />
                </span>
                <span>Recursos psicoeducativos</span>
              </div>
              <a
                href="#comenzar"
                className="inline-flex items-center gap-1 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity"
              >
                <span>Acceder</span>
                <span>→</span>
              </a>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
