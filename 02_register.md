# Page: Register

## Purpose
Let a new user create an account under one of three roles: Farmer, Equipment Owner, or Godown Owner.

## Layout
- Centered form card on a simple background (no distracting hero image)
- **Role selector at top**: 3 toggle tabs/pills — Farmer | Equipment Owner | Godown Owner (pre-selected if user arrived via a role card on landing page)
- Form fields: Full Name, Phone Number, Email (optional), Location (district/upazila dropdown or text), Password, Confirm Password
- Conditional field: if Equipment Owner or Godown Owner selected, show an extra "Business/Farm Name" field
- Primary button: "Create Account"
- Secondary link: "Already have an account? Login"

## Workflow
1. User selects role tab
2. Fills in required fields
3. Inline validation as they type (e.g. phone format, password match)
4. Clicks "Create Account" → success state → redirected to role-specific Dashboard
5. If validation fails, relevant field is highlighted with a short error message beneath it (no full-page error dump)

## Interactions
- Password field has a show/hide toggle
- Location field: searchable dropdown rather than free text, to keep data consistent
- Button shows a loading spinner briefly on submit, then success checkmark before redirect

## Design Notes
- Keep the form short — only ask what's needed for MVP roles
- Role tabs use distinct icons (tractor / warehouse / plant) so illiterate-friendly recognition is possible
- Mobile-first: single column, large input fields
