import { PrivateLayout } from "@/components/layout";

export default function AppRouteLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <PrivateLayout>{children}</PrivateLayout>;
}
