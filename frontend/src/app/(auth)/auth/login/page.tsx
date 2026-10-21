import { Suspense } from "react";
import { redirect } from "next/navigation";
import { validateSession } from "@/lib/session";
import { AuthLayoutShell, LoginForm } from "@/features/auth";

export const metadata = {
  title: "Iniciar sesión",
  description:
    "Acceso seguro al espacio personal de seguimiento del bienestar en Yusay.",
};

interface LoginPageProps {
  searchParams: Promise<{
    sessionExpired?: string;
  }>;
}

async function LoginContent({
  searchParams,
}: {
  searchParams: Promise<{ sessionExpired?: string }>;
}) {
  const resolvedParams = await searchParams;
  const sessionExpired = resolvedParams.sessionExpired === "true";

  return <LoginForm sessionExpired={sessionExpired} />;
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const session = await validateSession();
  if (session) {
    redirect("/inicio");
  }

  return (
    <AuthLayoutShell
      eyebrow="Acceso a tu espacio"
      title="Bienvenido de nuevo"
      subtitle="Continúa explorando tu bienestar, a tu ritmo."
      quote="“El bienestar no es una meta que conquistar, sino la cadencia tranquila con la que habitas tus días.”"
    >
      <Suspense fallback={<LoginForm sessionExpired={false} />}>
        <LoginContent searchParams={searchParams} />
      </Suspense>
    </AuthLayoutShell>
  );
}
