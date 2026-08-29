# Page: Login

## Purpose
Authenticate a returning user and route them to their role-specific dashboard.

## Layout
- Centered minimal card: Phone Number or Email field, Password field, "Login" button
- "Forgot password?" link under the password field
- "Don't have an account? Register" link at bottom
- No role selector needed — role is resolved from the account after login

## Workflow
1. User enters credentials
2. Clicks "Login"
3. Success → redirected straight to their role's Dashboard (Farmer / Equipment Owner / Godown Owner)
4. Failure → inline error message near the password field ("Incorrect phone number or password"), field border turns red, no page reload

## Interactions
- Show/hide password toggle
- Button disabled until both fields are filled
- Loading spinner on submit
- "Remember me" checkbox (optional, keeps session longer)

## Design Notes
- UI Theme: Agriculture-related green and white color palette — clean white card surface with agricultural green action buttons, green focus outlines, and fresh green branding
- Keep this page visually consistent with Register (same card style, same palette)
- Single column, generous spacing for mobile tapping
- No unnecessary marketing content on this page — pure utility
