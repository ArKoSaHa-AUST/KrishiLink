# Page: Godown Owner Dashboard

## Purpose
Landing page for a Godown Owner after login — overview of their storage listings and booking requests.

## Layout
- **Top navbar**: Logo, My Godowns, Booking Requests, Profile icon
- **Summary cards row**: Total Godowns, Occupied Capacity, Pending Booking Requests
- **"Add New Godown" button**: prominent, top-right of listings section
- **My godowns preview**: cards/list showing godown name, total vs. available capacity (progress bar), status
- **Pending requests widget**: incoming storage booking requests with farmer name, requested capacity, duration, Accept/Reject inline

## Workflow
1. Owner logs in → lands here
2. Checks capacity utilization at a glance via progress bars
3. Reviews and Accepts/Rejects pending booking requests inline
4. Clicks "Add New Godown" → routes to Godown Listing Form
5. Clicks an existing godown → routes to edit form

## Interactions
- Capacity progress bar changes color as it fills (green → amber near full → red when full)
- Pending requests widget mirrors the same inline Accept/Reject pattern as Equipment Owner Dashboard for consistency

## Design Notes
- Capacity visualization (progress bars) is the key differentiator from Equipment Owner Dashboard — make it the visual anchor
- Mobile: same horizontal-scroll summary cards pattern as Equipment Owner Dashboard
