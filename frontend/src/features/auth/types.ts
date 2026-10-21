export interface SignInResponse {
  userId: string;
  email: string;
  accessToken: string;
  tokenType: string;
  issuedAt: string;
  expiresAt: string;
}

export interface SignInActionResult {
  success: boolean;
  error?: string;
  fieldErrors?: {
    email?: string;
    password?: string;
  };
}

export interface RegisterResponse {
  id: string;
  email: string;
}

export interface RegisterActionResult {
  success: boolean;
  error?: string;
  email?: string;
  fieldErrors?: {
    email?: string;
    password?: string;
    confirmPassword?: string;
    adultConfirmed?: string;
  };
  values?: {
    email?: string;
    adultConfirmed?: boolean;
  };
}

export interface VerifyEmailResponse {
  userId: string;
  email: string;
  verifiedAt: string;
}

export type EmailVerificationState = "pending" | "verified" | "expired";

export interface VerifyEmailResult {
  state: EmailVerificationState;
  email?: string;
  error?: string;
}

export interface RequestPasswordResetResponse {
  emailSent: boolean;
}

export interface RequestPasswordResetActionResult {
  success: boolean;
  email?: string;
  error?: string;
  fieldErrors?: {
    email?: string;
  };
}

export interface ResetPasswordResponse {
  userId: string;
  email: string;
}

export interface ResetPasswordActionResult {
  success: boolean;
  error?: string;
  isExpiredToken?: boolean;
  fieldErrors?: {
    newPassword?: string;
    confirmNewPassword?: string;
  };
  values?: {
    newPassword?: string;
    confirmNewPassword?: string;
  };
}



