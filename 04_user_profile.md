# Page: User Profile

## Purpose
Let a logged-in user (any role) view and edit their personal details.

## Layout
- Left/top section: avatar placeholder (initials-based, no upload needed for MVP), Name, Role badge (Farmer/Equipment Owner/Godown Owner), Location
- Editable fields panel: Name, Phone, Email, Location, Password change section (collapsed by default, expands on click "Change Password")
- "Save Changes" button, disabled until a field is edited
- Tabs or side links (if space allows): Profile | My Listings/Bookings (role-dependent shortcut back to their dashboard)

## Workflow
1. User navigates here from a "Profile" icon/menu in the top navbar (present on all logged-in pages)
2. Views current info in read-only style
3. Clicks "Edit" → fields become editable
4. Makes changes → clicks "Save Changes" → success toast ("Profile updated") → fields return to read-only view

## Interactions
- Inline validation same as Register page
- "Change Password" expands an accordion with Current Password / New Password / Confirm New Password
- Success/error feedback via toast notification, not full page reload

## Design Notes
- UI Theme: Agriculture-related green and white color palette — crisp white profile cards, green role badges, green button accents, and clean white layout
- Keep this page simple and utilitarian — it's not a frequently visited page
- Role badge uses the same icon/color convention as Register page tabs for consistency
