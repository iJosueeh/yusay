import { redirect } from "next/navigation";
import { validateSession } from "@/lib/session";
import { AuthLayoutShell, RegisterForm } from "@/features/auth";
import toolsImg3 from "@/assets/tools-img-3.png";

export const metadata = {
  title: "Crear cuenta",
  description:
    "Crea tu espacio personal para registrar y observar tu bienestar en Yusay.",
};

export default async function RegisterPage() {
  const session = await validateSession();
  if (session) {
    redirect("/inicio");
  }

  return (
    <AuthLayoutShell
      eyebrow="Registro confidencial"
      title="Comienza a tu ritmo"
      subtitle="Crea tu espacio personal para registrar y observar tu bienestar."
      quote="«Nombrar lo que sentimos no requiere perfección métrica, sino un instante de honesta calma.»"
      image={toolsImg3}
      imageAlt="Mujer sentada cómodamente en un sillón junto a una ventana luminosa, escribiendo en su cuaderno con serenidad"
    >
      <RegisterForm />
    </AuthLayoutShell>
  );
}
