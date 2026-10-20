import { ShieldCheck, Lock, EyeOff, Trash2 } from "lucide-react";
import { Reveal } from "@/components/ui/reveal";

export function PrivacySection() {
  return (
    <section
      id="privacidad"
      className="bg-forest text-canvas py-16 lg:py-24 w-full"
    >
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-12 items-center">
          {/* Bloque de compromiso ético (7 cols) */}
          <Reveal
            delay={0.1}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-7 space-y-6"
          >
            <div className="inline-flex items-center gap-2 bg-forest-hover/80 px-3.5 py-1.5 rounded-full text-sage border border-sage/20">
              <ShieldCheck className="h-4 w-4 text-sage" />
              <span className="text-xs uppercase tracking-widest font-semibold font-sans">
                Compromiso Ético
              </span>
            </div>

            <h2 className="font-serif text-3xl sm:text-4xl lg:text-[2.75rem] text-canvas leading-tight font-normal">
              Un entorno reservado donde tus datos son solo tuyos
            </h2>

            <p className="text-base sm:text-lg text-canvas/85 leading-relaxed font-sans">
              Concebimos Yusay con la firme convicción de que el bienestar
              personal requiere un espacio de sosiego, sobriedad y discreción
              absoluta.
            </p>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-5 pt-3 border-t border-sage/20">
              {/* 1. Privacidad */}
              <div className="space-y-1.5 p-3 rounded-xl hover:bg-forest-hover/40 transition-colors">
                <div className="flex items-center gap-2 text-sage">
                  <Lock className="h-4 w-4" />
                  <span className="font-serif text-lg text-canvas block">
                    Privacidad y reserva
                  </span>
                </div>
                <p className="text-xs text-canvas/70 leading-relaxed font-sans">
                  Recolección mínima de datos (zero-PII): sin nombres ni datos
                  superfluos. Tus registros son de acceso exclusivo para ti.
                </p>
              </div>

              {/* 2. Sin publicidad */}
              <div className="space-y-1.5 p-3 rounded-xl hover:bg-forest-hover/40 transition-colors">
                <div className="flex items-center gap-2 text-sage">
                  <EyeOff className="h-4 w-4" />
                  <span className="font-serif text-lg text-canvas block">
                    Sin publicidad
                  </span>
                </div>
                <p className="text-xs text-canvas/70 leading-relaxed font-sans">
                  No vendemos tu atención, no incluimos rastreadores
                  comerciales ni cedemos información a terceros con fines
                  publicitarios.
                </p>
              </div>

              {/* 3. No clínico */}
              <div className="space-y-1.5 p-3 rounded-xl hover:bg-forest-hover/40 transition-colors">
                <div className="flex items-center gap-2 text-sage">
                  <ShieldCheck className="h-4 w-4" />
                  <span className="font-serif text-lg text-canvas block">
                    Alcance no clínico
                  </span>
                </div>
                <p className="text-xs text-canvas/70 leading-relaxed font-sans">
                  Herramienta de autoobservación y orientación. No emite
                  diagnósticos médicos ni sustituye la terapia profesional.
                </p>
              </div>

              {/* 4. Control de cuenta */}
              <div className="space-y-1.5 p-3 rounded-xl hover:bg-forest-hover/40 transition-colors">
                <div className="flex items-center gap-2 text-sage">
                  <Trash2 className="h-4 w-4" />
                  <span className="font-serif text-lg text-canvas block">
                    Control sobre tu cuenta
                  </span>
                </div>
                <p className="text-xs text-canvas/70 leading-relaxed font-sans">
                  Autonomía total: puedes solicitar la eliminación física
                  definitiva de tu cuenta y todos sus registros en cualquier
                  momento.
                </p>
              </div>
            </div>
          </Reveal>

          {/* Cuadro Testimonial de Principios (5 cols) */}
          <Reveal
            delay={0.2}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-5"
          >
            <div className="bg-forest-hover/90 border border-sage/20 p-8 rounded-2xl space-y-6 shadow-md">
              <span className="text-[0.6875rem] uppercase tracking-widest text-sage block font-semibold font-sans">
                Principio Fundacional
              </span>

              <p className="font-serif italic text-xl text-canvas/95 leading-relaxed">
                «Una plataforma de bienestar debe ofrecer calma y claridad,
                respetando siempre la autonomía, la dignidad y el ritmo de quien
                la utiliza.»
              </p>

              <div className="pt-4 border-t border-sage/20 flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-sage/20 border border-sage/30 flex items-center justify-center font-serif text-base text-sage font-medium">
                  Y
                </div>
                <div>
                  <span className="text-xs font-semibold uppercase tracking-wider block text-canvas font-sans">
                    Principios de Yusay
                  </span>
                  <span className="text-[0.6875rem] text-canvas/60 font-sans">
                    Diseño reflexivo para el bienestar personal
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
