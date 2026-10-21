import Link from "next/link";

export function PublicFooter() {
  return (
    <footer className="w-full bg-forest-depths border-t border-snow-white/10 text-snow-white py-14 lg:py-18">
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        <div className="grid grid-cols-1 md:grid-cols-12 gap-8 lg:gap-12">
          {/* Identidad de Marca y Aviso No Clínico */}
          <div className="md:col-span-5 space-y-3.5">
            <div className="flex items-center gap-2">
              <span className="font-serif text-2xl text-snow-white font-light">
                Yusay
              </span>
              <span
                className="w-1.5 h-1.5 rounded-full bg-lime-pulse mb-0.5"
                aria-hidden="true"
              />
            </div>
            <p className="text-sm text-snow-white/70 max-w-sm leading-relaxed font-sans">
              Plataforma digital para el seguimiento estructurado del bienestar
              personal, evaluaciones periódicas y recursos psicoeducativos.
            </p>
            <div className="pt-2">
              <p className="text-xs text-snow-white/80 leading-relaxed max-w-md bg-snow-white/5 p-3.5 rounded-lg border border-snow-white/15 shadow-none">
                <strong className="font-medium text-snow-white">Aviso importante:</strong>{" "}
                Yusay no constituye un servicio de atención clínica ni sustituye
                el acompañamiento médico, psiquiátrico o psicoterapéutico profesional.
                En situaciones de crisis o malestar agudo, consulta siempre con
                profesionales de la salud.
              </p>
            </div>
          </div>

          {/* Enlaces: Plataforma */}
          <div className="md:col-span-3 space-y-3">
            <h4 className="text-xs font-mono uppercase tracking-wider text-snow-white/90 font-medium">
              Plataforma
            </h4>
            <ul className="space-y-2 text-sm text-snow-white/70 font-sans">
              <li>
                <a
                  href="#concepto"
                  className="hover:text-snow-white hover:underline underline-offset-4 transition-colors"
                >
                  El Concepto
                </a>
              </li>
              <li>
                <a
                  href="#metodo"
                  className="hover:text-snow-white hover:underline underline-offset-4 transition-colors"
                >
                  Cómo funciona
                </a>
              </li>
              <li>
                <a
                  href="#herramientas"
                  className="hover:text-snow-white hover:underline underline-offset-4 transition-colors"
                >
                  Herramientas
                </a>
              </li>
              <li>
                <Link
                  href="/auth/registro"
                  className="hover:text-snow-white hover:underline underline-offset-4 transition-colors"
                >
                  Crear cuenta
                </Link>
              </li>
            </ul>
          </div>

          {/* Enlaces: Compromiso y Privacidad */}
          <div className="md:col-span-4 space-y-3">
            <h4 className="text-xs font-mono uppercase tracking-wider text-snow-white/90 font-medium">
              Compromiso y Privacidad
            </h4>
            <ul className="space-y-2 text-sm text-snow-white/70 font-sans">
              <li>
                <a
                  href="#privacidad"
                  className="hover:text-snow-white hover:underline underline-offset-4 transition-colors"
                >
                  Privacidad y Datos
                </a>
              </li>
              <li>
                <span
                  className="text-snow-white/50 cursor-default inline-flex items-center gap-1.5"
                  title="Documento formal en preparación legal"
                >
                  <span>Términos de uso</span>
                  <span className="text-[10px] uppercase font-mono px-2 py-0.5 rounded-full bg-snow-white/10 border border-snow-white/20 text-snow-white/70">
                    Pendiente
                  </span>
                </span>
              </li>
              <li>
                <span
                  className="text-snow-white/50 cursor-default inline-flex items-center gap-1.5"
                  title="Documento formal en preparación legal"
                >
                  <span>Aviso legal completo</span>
                  <span className="text-[10px] uppercase font-mono px-2 py-0.5 rounded-full bg-snow-white/10 border border-snow-white/20 text-snow-white/70">
                    Pendiente
                  </span>
                </span>
              </li>
            </ul>
          </div>
        </div>

        {/* Créditos y Derechos */}
        <div className="mt-12 pt-6 border-t border-snow-white/10 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-snow-white/60 font-mono">
          <p>© 2026 Yusay. Todos los derechos reservados.</p>
          <p className="font-serif italic text-sm text-snow-white/80 font-light">
            Bienestar personal sin juicios ni exigencias
          </p>
        </div>
      </div>
    </footer>
  );
}
