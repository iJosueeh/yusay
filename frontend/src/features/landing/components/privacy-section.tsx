import Link from "next/link";
import { ShieldCheck, Lock, EyeOff, Trash2 } from "lucide-react";
import { Reveal } from "@/components/ui/reveal";

export function PrivacySection() {
  return (
    <section
      id="privacidad"
      className="bg-forest-depths text-snow-white py-20 lg:py-28 w-full"
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
            <div className="inline-flex items-center gap-2 bg-snow-white/10 px-3.5 py-1.5 rounded-full text-snow-white border border-snow-white/20 font-mono text-xs uppercase tracking-wider">
              <ShieldCheck className="h-4 w-4 text-snow-white" />
              <span>Compromiso Ético</span>
            </div>

            <h2 className="font-serif text-3xl sm:text-4xl lg:text-[2.75rem] text-snow-white leading-tight font-light tracking-tight">
              Un entorno reservado donde tus datos son solo tuyos
            </h2>

            <p className="text-base sm:text-lg text-snow-white/80 leading-relaxed font-sans">
              Concebimos Yusay con la firme convicción de que el bienestar
              personal requiere un espacio de sosiego, sobriedad y discreción
              absoluta.
            </p>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 sm:gap-5 pt-3 border-t border-snow-white/15">
              {/* 1. Privacidad */}
              <div className="p-4 rounded-xl border border-snow-white/15 bg-snow-white/5 hover:bg-snow-white/10 transition-colors shadow-none">
                <div className="flex items-center gap-3 mb-2">
                  <div className="w-8 h-8 rounded-lg bg-snow-white/10 border border-snow-white/20 flex items-center justify-center text-snow-white">
                    <Lock className="h-4 w-4" />
                  </div>
                  <span className="font-serif text-lg text-snow-white font-light block">
                    Privacidad y reserva
                  </span>
                </div>
                <p className="text-xs text-snow-white/75 leading-relaxed font-sans">
                  Privacidad total: no solicitamos nombres, apellidos ni datos
                  personales innecesarios. Tu espacio es confidencial y solo tú
                  puedes acceder a él.
                </p>
              </div>

              {/* 2. Sin publicidad */}
              <div className="p-4 rounded-xl border border-snow-white/15 bg-snow-white/5 hover:bg-snow-white/10 transition-colors shadow-none">
                <div className="flex items-center gap-3 mb-2">
                  <div className="w-8 h-8 rounded-lg bg-snow-white/10 border border-snow-white/20 flex items-center justify-center text-snow-white">
                    <EyeOff className="h-4 w-4" />
                  </div>
                  <span className="font-serif text-lg text-snow-white font-light block">
                    Sin publicidad
                  </span>
                </div>
                <p className="text-xs text-snow-white/75 leading-relaxed font-sans">
                  No vendemos tu atención, no incluimos rastreadores
                  comerciales ni cedemos información a terceros con fines
                  publicitarios.
                </p>
              </div>

              {/* 3. No clínico */}
              <div className="p-4 rounded-xl border border-snow-white/15 bg-snow-white/5 hover:bg-snow-white/10 transition-colors shadow-none">
                <div className="flex items-center gap-3 mb-2">
                  <div className="w-8 h-8 rounded-lg bg-snow-white/10 border border-snow-white/20 flex items-center justify-center text-snow-white">
                    <ShieldCheck className="h-4 w-4" />
                  </div>
                  <span className="font-serif text-lg text-snow-white font-light block">
                    Alcance no clínico
                  </span>
                </div>
                <p className="text-xs text-snow-white/75 leading-relaxed font-sans">
                  Herramienta de autoobservación y orientación. No emite
                  diagnósticos médicos ni sustituye la terapia profesional.
                </p>
              </div>

              {/* 4. Control de cuenta */}
              <div className="p-4 rounded-xl border border-snow-white/15 bg-snow-white/5 hover:bg-snow-white/10 transition-colors shadow-none">
                <div className="flex items-center gap-3 mb-2">
                  <div className="w-8 h-8 rounded-lg bg-snow-white/10 border border-snow-white/20 flex items-center justify-center text-snow-white">
                    <Trash2 className="h-4 w-4" />
                  </div>
                  <span className="font-serif text-lg text-snow-white font-light block">
                    Control sobre tu cuenta
                  </span>
                </div>
                <p className="text-xs text-snow-white/75 leading-relaxed font-sans">
                  Autonomía total: puedes solicitar la eliminación física
                  definitiva de tu cuenta y todos sus registros en cualquier
                  momento.
                </p>
              </div>
            </div>

            <div className="pt-2">
              <Link
                href="/auth/registro"
                className="inline-flex items-center gap-2 text-xs font-mono uppercase tracking-wider text-snow-white border-b-[1.5px] border-snow-white pb-0.5 hover:opacity-75 transition-opacity"
              >
                <span>Crear tu espacio privado sin compartir datos personales</span>
                <span>→</span>
              </Link>
            </div>
          </Reveal>

          {/* Cuadro Testimonial de Principios (5 cols, shadowless) */}
          <Reveal
            delay={0.2}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-5"
          >
            <div className="relative overflow-hidden bg-snow-white/5 border border-snow-white/15 p-8 rounded-2xl space-y-6 shadow-none">
              <span className="text-[0.6875rem] uppercase tracking-widest text-snow-white/70 block font-mono font-medium">
                Principio Fundacional
              </span>

              <p className="font-serif italic text-xl text-snow-white leading-relaxed font-light">
                «Una plataforma de bienestar debe ofrecer calma y claridad,
                respetando siempre la autonomía, la dignidad y el ritmo de quien
                la utiliza.»
              </p>

              <div className="pt-4 border-t border-snow-white/15 flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-snow-white/10 border border-snow-white/20 flex items-center justify-center font-serif text-base text-snow-white font-light">
                  Y
                </div>
                <div>
                  <span className="text-xs font-mono uppercase tracking-wider block text-snow-white">
                    Principios de Yusay
                  </span>
                  <span className="text-[0.6875rem] text-snow-white/60 font-sans">
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
