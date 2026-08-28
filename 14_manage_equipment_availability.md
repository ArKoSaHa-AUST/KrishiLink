# Page: Manage Equipment Availability (Equipment Owner)

## Purpose
Let an owner mark specific dates as available/unavailable for a piece of equipment, separate from active bookings.

## Layout
- Equipment name/summary at top (with thumbnail)
- **Calendar view**: month grid, dates color-coded — Available (green/default), Unavailable/blocked by owner (gray), Booked by a farmer (blue, non-editable)
- Toggle or click-to-select dates, then a small action bar appears: "Mark as Unavailable" / "Mark as Available"
- "Save Changes" button

## Workflow
1. Owner navigates here from "Availability" action on a listing (Dashboard or Listing edit page)
2. Clicks/taps one or more calendar dates
3. Selects "Mark as Unavailable" (e.g. equipment under maintenance) or reverts to available
4. Saves — calendar updates immediately, confirmation toast shown

## Interactions
- Booked dates are locked (not clickable) since they're tied to accepted rentals
- Multi-select via click-drag or shift-click on desktop; tap-and-drag on mobile
- Legend shown above calendar explaining the color coding

## Design Notes
- Keep the calendar the clear visual focus of the page — minimal surrounding chrome
- Reuse the same calendar component style as the farmer-facing availability calendar on Equipment Detail page, for visual consistency
