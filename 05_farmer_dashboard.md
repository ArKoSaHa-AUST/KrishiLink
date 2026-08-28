# Page: Farmer Dashboard

## Purpose
Landing page after a Farmer logs in — a single overview of everything relevant to them, and the launch point for all farmer actions.

## Layout
- **Top navbar**: Logo, links to Equipment, Godowns, Crop Advisory, Bookings, Profile icon
- **Quick action cards** (row of 3): "Find Equipment", "Find Storage", "Get Crop Advice" — each a large clickable card with icon
- **Active bookings summary**: small table/list showing current equipment rentals & godown bookings with status badges (Pending / Accepted / Completed)
- **Recommended crops widget** (if farmer has used Crop Advisory before): shows last recommendation with a "View Full Guide" link
- **Recent activity feed**: last few actions (e.g. "Request sent for Tractor #12", "Godown booking accepted")

## Workflow
1. Farmer logs in → lands here
2. Scans quick action cards → clicks into Equipment Browse, Godown Browse, or Crop Advisory
3. Checks active bookings status at a glance without leaving the dashboard
4. Clicks a booking row → jumps to Booking History page detail for that item

## Interactions
- Status badges color-coded: yellow (Pending), green (Accepted), gray (Completed), red (Rejected)
- Cards have hover animation (slight scale-up) to feel interactive
- Empty states designed clearly (e.g. "No active bookings yet — start by finding equipment" with a CTA button)

## Design Notes
- **UI Theme**: Follow the farmer-focused agricultural aesthetic — **green (`--krishi-primary: #2d6a4f`) as the dominant accent** (navbar, buttons, active states, icons) and **white/off-white (`--krishi-bg: #f8faf7`) as the base background**. All colors are defined as CSS custom properties in `wwwroot/css/site.css`; use them everywhere instead of hardcoding hex values. Avoid corporate blue entirely — this should feel warm, natural, and trustworthy.
- Use the shared `_Layout.cshtml` (sticky green navbar, green footer), `_EquipmentCard.cshtml` partial for any equipment/godown preview cards, `_StatusBadge.cshtml` for all status indicators, and `btn-krishi-primary` / `btn-krishi-outline` for buttons.
- This is the most-visited page for farmers — prioritize clarity over density
- Mobile: quick action cards stack vertically full-width; activity feed becomes a simple scrollable list
