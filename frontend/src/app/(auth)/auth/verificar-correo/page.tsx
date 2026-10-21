import { Suspense } from "react";
import {
  AuthLayoutShell,
  EmailVerificationView,
  EmailVerificationSkeleton,
  verifyEmailToken,
  type EmailVerificationState,
} from "@/features/auth";

export const metadata = {
  title: "Verificación de cuenta",
  description:
    "Verifica tu dirección de correo electrónico para acceder a tu espacio personal de bienestar en Yusay.",
};

interface VerificarCorreoPageProps {
  searchParams: Promise<{
    token?: string;
  }>;
}

async function EmailVerificationContent({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const resolvedParams = await searchParams;
  const token = resolvedParams.token;
  const hasToken = Boolean(token && token.trim().length > 0);

  let state: EmailVerificationState = "pending";

  if (hasToken) {
    const result = await verifyEmailToken(token);
    state = result.state;
  }

  return <EmailVerificationView state={state} hasTokenInUrl={hasToken} />;
}

export default function VerificarCorreoPage({
  searchParams,
}: VerificarCorreoPageProps) {
  return (
    <AuthLayoutShell
      quote="«El bienestar no es una meta que conquistar, sino la cadencia tranquila con la que habitas tus días.»"
      imageAlt="Momento de calma con luz natural y taza de té"
    >
      <Suspense fallback={<EmailVerificationSkeleton />}>
        <EmailVerificationContent searchParams={searchParams} />
      </Suspense>
    </AuthLayoutShell>
  );
}
