import "server-only";

/**
 * Configuración de entorno server-side para el BFF de Yusay.
 * Todas las variables aquí definidas son accesibles exclusivamente
 * en el entorno Node.js del servidor y jamás se exponen al cliente navegador.
 */
export const serverConfig = {
  backendApiUrl:
    process.env.BACKEND_API_URL?.replace(/\/+$/, "") || "http://localhost:5000",
} as const;
