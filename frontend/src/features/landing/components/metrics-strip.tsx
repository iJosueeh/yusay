import { Reveal } from "@/components/ui/reveal";

interface MetricItem {
  tag: string;
  value: string;
  title: string;
  description: string;
}

const METRICS: MetricItem[] = [
  {
    tag: "Tu propio ritmo",
    value: "168h",
    title: "Ventana flexible",
    description: "Hasta 7 días para completar o editar tus notas diarias sin penalizaciones por días de pausa.",
  },
  {
    tag: "Observación diaria",
    value: "4",
    title: "Aspectos clave",
    description: "Ánimo, estrés, energía y sueño en una escala sencilla del 1 al 5 en menos de un minuto.",
  },
  {
    tag: "Privacidad total",
    value: "0",
    title: "Sin datos personales",
    description: "No te pedimos nombres reales ni teléfonos. Sin anuncios, rastreadores ni venta de datos.",
  },
  {
    tag: "Acompañamiento sereno",
    value: "100%",
    title: "Enfoque respetuoso",
    description: "Un espacio de autoconocimiento reflexivo, concebido para cuidarte sin diagnósticos médicos.",
  },
];

export function MetricsStrip() {
  return (
    <section
      aria-label="Parámetros y métricas de diseño de Yusay"
      className="w-full border-y border-warm-stone bg-snow-white py-12 lg:py-16"
    >
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-8 lg:gap-10">
          {METRICS.map((metric, index) => (
            <Reveal
              key={metric.value}
              delay={index * 0.08}
              duration={0.4}
              yOffset={12}
              className="flex flex-col justify-between pt-4 sm:pt-0 border-t sm:border-t-0 sm:border-l first:border-l-0 border-warm-stone sm:pl-6 lg:pl-8 first:pl-0"
            >
              <div>
                <span className="font-mono text-[10px] uppercase tracking-widest text-pewter block mb-3 font-medium">
                  {metric.tag}
                </span>

                <div className="font-serif text-4xl sm:text-5xl lg:text-[3.5rem] font-light text-forest-depths tracking-tight leading-none mb-2">
                  {metric.value}
                </div>

                <h3 className="font-serif text-lg text-forest-depths font-light tracking-tight mb-1.5">
                  {metric.title}
                </h3>

                <p className="font-sans text-xs text-pewter leading-relaxed">
                  {metric.description}
                </p>
              </div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}
