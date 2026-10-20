import Link from "next/link";

export function PublicFooter() {
  return (
    <footer className="w-full bg-sage-subtle/70 border-t border-sage/40 text-midnight py-12 lg:py-16">
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        <div className="grid grid-cols-1 md:grid-cols-12 gap-8 lg:gap-12">
          {/* Identidad de Marca y Aviso No Clínico */}
          <div className="md:col-span-5 space-y-3.5">
            <div className="flex items-center gap-2">
              <span className="font-serif text-2xl text-forest font-normal">
                Yusay
              </span>
              <span
                className="w-1.5 h-1.5 rounded-full bg-terracotta mb-0.5"
                aria-hidden="true"
              />
            </div>
            <p className="text-sm text-midnight-muted max-w-sm leading-relaxed">
              Plataforma digital para el seguimiento estructurado del bienestar
              personal, evaluaciones periódicas y recursos psicoeducativos.
            </p>
            <div className="pt-2">
              <p className="text-xs text-midnight-muted/90 leading-relaxed max-w-md bg-canvas/60 p-3 rounded-lg border border-sage/30">
                <strong className="font-medium text-midnight">Aviso importante:</strong>{" "}
                Yusay no constituye un servicio de atención clínica ni sustituye
                el acompañamiento médico, psiquiátrico o psicoterapéutico profesional.
                En situaciones de crisis o malestar agudo, consulta siempre con
                profesionales de la salud.
              </p>
            </div>
          </div>

          {/* Enlaces: Plataforma */}
          <div className="md:col-span-3 space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-forest">
              Plataforma
            </h4>
            <ul className="space-y-2 text-sm text-midnight-muted">
              <li>
                <a
                  href="#concepto"
                  className="hover:text-forest hover:underline underline-offset-4 transition-colors"
                >
                  El Concepto
                </a>
              </li>
              <li>
                <a
                  href="#metodo"
                  className="hover:text-forest hover:underline underline-offset-4 transition-colors"
                >
                  Cómo funciona
                </a>
              </li>
              <li>
                <a
                  href="#herramientas"
                  className="hover:text-forest hover:underline underline-offset-4 transition-colors"
                >
                  Herramientas
                </a>
              </li>
              <li>
                <Link
                  href="/auth/registro"
                  className="hover:text-forest hover:underline underline-offset-4 transition-colors"
                >
                  Crear cuenta
                </Link>
              </li>
            </ul>
          </div>

          {/* Enlaces: Compromiso y Privacidad */}
          <div className="md:col-span-4 space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-forest">
              Compromiso y Privacidad
            </h4>
            <ul className="space-y-2 text-sm text-midnight-muted">
              <li>
                <a
                  href="#privacidad"
                  className="hover:text-forest hover:underline underline-offset-4 transition-colors"
                >
                  Privacidad y Datos
                </a>
              </li>
              <li>
                <span
                  className="text-midnight-subtle cursor-default inline-flex items-center gap-1.5"
                  title="Documento formal en preparación legal"
                >
                  <span>Términos de uso</span>
                  <span className="text-[10px] uppercase tracking-wider px-1.5 py-0.5 rounded bg-sage-light text-midnight-muted">
                    Pendiente
                  </span>
                </span>
              </li>
              <li>
                <span
                  className="text-midnight-subtle cursor-default inline-flex items-center gap-1.5"
                  title="Documento formal en preparación legal"
                >
                  <span>Aviso legal completo</span>
                  <span className="text-[10px] uppercase tracking-wider px-1.5 py-0.5 rounded bg-sage-light text-midnight-muted">
                    Pendiente
                  </span>
                </span>
              </li>
            </ul>
          </div>
        </div>

        {/* Créditos y Derechos */}
        <div className="mt-12 pt-6 border-t border-sage/40 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-midnight-muted">
          <p>© 2026 Yusay. Todos los derechos reservados.</p>
          <p className="font-serif italic text-sm text-forest">
            Tipografías Newsreader &amp; DM Sans
          </p>
        </div>
      </div>
    </footer>
  );
}
