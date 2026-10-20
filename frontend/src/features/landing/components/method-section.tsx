import { PenLine, TrendingUp, BookOpen } from "lucide-react";
import { Reveal } from "@/components/ui/reveal";

export function MethodSection() {
  return (
    <section
      id="metodo"
      className="py-14 lg:py-20 max-w-7xl mx-auto px-6 lg:px-12 w-full"
    >
      {/* Cabecera de sección */}
      <Reveal
        duration={0.5}
        yOffset={16}
        className="flex flex-col md:flex-row md:items-end justify-between gap-4 mb-12"
      >
        <div className="space-y-2 max-w-xl">
          <span className="text-xs font-semibold uppercase tracking-widest text-forest">
            Método de uso
          </span>
          <h2 className="font-serif text-3xl sm:text-4xl text-forest font-normal">
            Un proceso sencillo integrado en tu día a día
          </h2>
        </div>
        <p className="text-sm text-midnight-muted max-w-sm">
          Tres momentos complementarios pensados para encajar con naturalidad en tu
          rutina, sin exigir esfuerzo excesivo ni generar ansiedad.
        </p>
      </Reveal>

      {/* Tarjetas de las tres etapas con entrada escalonada y hovers mejorados */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 lg:gap-8">
        {/* 01 Registrar */}
        <Reveal
          delay={0.1}
          duration={0.5}
          yOffset={16}
          className="flex flex-col h-full"
        >
          <div className="bg-white/90 border border-sage/40 p-7 sm:p-8 rounded-2xl shadow-xs flex flex-col justify-between h-full transition-all duration-300 hover:border-forest/60 hover:shadow-lg hover:-translate-y-1.5 group motion-reduce:hover:transform-none motion-reduce:transition-none">
            <div className="space-y-3.5">
              <div className="flex items-center justify-between">
                <span className="font-serif italic text-3xl text-terracotta group-hover:scale-110 group-hover:text-terracotta-hover transition-transform duration-300 motion-reduce:transform-none">
                  01
                </span>
                <span className="text-[0.6875rem] uppercase tracking-wider text-forest bg-sage-light/80 px-2.5 py-1 rounded-full font-sans font-semibold">
                  Pausa de 1 minuto
                </span>
              </div>
              <h3 className="font-serif text-2xl text-forest font-normal">
                Check-in consciente
              </h3>
              <p className="font-sans text-sm text-midnight-muted leading-relaxed">
                Dedica una pausa breve al final de la jornada para notar tus sensaciones
                en cuatro dimensiones clave: ánimo, estrés, energía y sueño. Escala 1 a 5
                intuitiva, sin fórmulas forzadas y con espacio para notas reflexivas.
              </p>
            </div>
            <div className="pt-5 border-t border-sage/20 mt-6 flex items-center gap-2 text-xs uppercase tracking-wider text-forest font-semibold">
              <span className="p-1 rounded-md bg-sage-subtle group-hover:bg-forest/10 transition-colors">
                <PenLine className="h-4 w-4 text-forest group-hover:rotate-6 transition-transform motion-reduce:transform-none" />
              </span>
              <span>Check-in diario</span>
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
          <div className="bg-sage-subtle/50 border border-sage/40 p-7 sm:p-8 rounded-2xl shadow-xs flex flex-col justify-between h-full transition-all duration-300 hover:border-forest/60 hover:shadow-lg hover:-translate-y-1.5 group motion-reduce:hover:transform-none motion-reduce:transition-none">
            <div className="space-y-3.5">
              <div className="flex items-center justify-between">
                <span className="font-serif italic text-3xl text-terracotta group-hover:scale-110 group-hover:text-terracotta-hover transition-transform duration-300 motion-reduce:transform-none">
                  02
                </span>
                <span className="text-[0.6875rem] uppercase tracking-wider text-midnight-muted bg-white px-2.5 py-1 rounded-full font-sans font-medium border border-sage/30">
                  Seguimiento Visual
                </span>
              </div>
              <h3 className="font-serif text-2xl text-forest font-normal">
                Observar tendencias
              </h3>
              <p className="font-sans text-sm text-midnight-muted leading-relaxed">
                Visualiza la evolución temporal de tus registros y cuestionarios a lo
                largo de las semanas. Patrones continuos presentados con claridad para
                comprender tus ritmos, sin juicios de rendimiento.
              </p>
            </div>
            <div className="pt-5 border-t border-sage/20 mt-6 flex items-center gap-2 text-xs uppercase tracking-wider text-forest font-semibold">
              <span className="p-1 rounded-md bg-white group-hover:bg-forest/10 transition-colors">
                <TrendingUp className="h-4 w-4 text-forest group-hover:rotate-6 transition-transform motion-reduce:transform-none" />
              </span>
              <span>Historial y tendencias</span>
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
          <div className="bg-white/90 border border-sage/40 p-7 sm:p-8 rounded-2xl shadow-xs flex flex-col justify-between h-full transition-all duration-300 hover:border-forest/60 hover:shadow-lg hover:-translate-y-1.5 group motion-reduce:hover:transform-none motion-reduce:transition-none">
            <div className="space-y-3.5">
              <div className="flex items-center justify-between">
                <span className="font-serif italic text-3xl text-terracotta group-hover:scale-110 group-hover:text-terracotta-hover transition-transform duration-300 motion-reduce:transform-none">
                  03
                </span>
                <span className="text-[0.6875rem] uppercase tracking-wider text-midnight-muted bg-sage-subtle px-2.5 py-1 rounded-full font-sans font-medium border border-sage/30">
                  Orientación Guiada
                </span>
              </div>
              <h3 className="font-serif text-2xl text-forest font-normal">
                Comprender y cuidar
              </h3>
              <p className="font-sans text-sm text-midnight-muted leading-relaxed">
                Accede a una biblioteca de materiales psicoeducativos curados para
                profundizar en descanso, gestión del estrés y autocuidado con
                fundamento contrastado y lenguaje cercano.
              </p>
            </div>
            <div className="pt-5 border-t border-sage/20 mt-6 flex items-center gap-2 text-xs uppercase tracking-wider text-forest font-semibold">
              <span className="p-1 rounded-md bg-sage-subtle group-hover:bg-forest/10 transition-colors">
                <BookOpen className="h-4 w-4 text-forest group-hover:rotate-6 transition-transform motion-reduce:transform-none" />
              </span>
              <span>Recursos psicoeducativos</span>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
