import {
  AuthLayoutShell,
  RequestPasswordResetForm,
} from "@/features/auth";

export const metadata = {
  title: "Recuperar contraseña",
  description:
    "Solicita el restablecimiento seguro de tu contraseña de acceso a Yusay.",
};

export default function RecuperarContrasenaPage() {
  return (
    <AuthLayoutShell
      eyebrow="Seguridad de la cuenta"
      title="Recuperar contraseña"
      subtitle="Introduce el correo electrónico asociado a tu cuenta para recibir las instrucciones de restablecimiento."
      quote="«La tranquilidad de saber que tu espacio personal permanece protegido.»"
      quoteAuthor="Seguridad Yusay"
    >
      <RequestPasswordResetForm />
    </AuthLayoutShell>
  );
}
