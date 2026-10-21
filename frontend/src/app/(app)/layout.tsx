import { redirect } from "next/navigation";
import { getSessionToken, validateSession } from "@/lib/session";
import { PrivateLayout } from "@/components/layout";

export default async function AppRouteLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const token = await getSessionToken();
  if (!token) {
    redirect("/auth/login");
  }

  const session = await validateSession();
  if (!session) {
    redirect("/auth/login?sessionExpired=true");
  }

  return <PrivateLayout>{children}</PrivateLayout>;
}
