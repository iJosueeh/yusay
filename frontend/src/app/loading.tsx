export default function Loading() {
  return (
    <div
      role="status"
      aria-live="polite"
      className="min-h-[50vh] flex flex-col items-center justify-center gap-4 p-8 text-center"
    >
      <div className="relative flex items-center justify-center">
        <div
          className="w-12 h-12 rounded-full border-2 border-sage border-t-forest animate-spin motion-reduce:animate-none"
          aria-hidden="true"
        />
        <span
          className="absolute font-serif text-xs text-forest select-none"
          aria-hidden="true"
        >
          🌿
        </span>
      </div>

      <p className="font-serif italic text-base text-forest">
        Preparando tu espacio...
      </p>
      <span className="sr-only">Cargando contenido de la página</span>
    </div>
  );
}
