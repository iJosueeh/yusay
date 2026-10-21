import { Suspense } from "react";
import {
  AuthLayoutShell,
  ResetPasswordForm,
  EmailVerificationSkeleton,
} from "@/features/auth";
import toolsImg3 from "@/assets/tools-img-3.png";

export const metadata = {
  title: "Crea una nueva contraseña",
  description:
    "Establece una nueva contraseña segura para acceder a tu espacio personal en Yusay.",
};

interface NuevaContrasenaPageProps {
  searchParams: Promise<{
    token?: string;
  }>;
}

async function NuevaContrasenaContent({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const resolvedParams = await searchParams;
  return <ResetPasswordForm token={resolvedParams.token} />;
}

export default function NuevaContrasenaPage({
  searchParams,
}: NuevaContrasenaPageProps) {
  return (
    <AuthLayoutShell
      image={toolsImg3}
      imageAlt="Persona reflexionando y escribiendo en su cuaderno sobre una mesa con luz matutina"
      quote="«Nombrar lo que sentimos no requiere perfección métrica, sino un instante de honesta calma.»"
    >
      <Suspense fallback={<EmailVerificationSkeleton />}>
        <NuevaContrasenaContent searchParams={searchParams} />
      </Suspense>
    </AuthLayoutShell>
  );
}
