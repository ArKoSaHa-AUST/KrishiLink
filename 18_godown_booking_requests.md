# Page: Godown Booking Requests (Godown Owner)

## Purpose
Full list of incoming storage booking requests where the owner can review and Accept/Reject.

## Layout
- **Filter tabs**: Pending | Accepted | Rejected | Completed
- **Request list/table**: farmer name, godown name, requested capacity, duration, note, status, Accept/Reject buttons (Pending tab only)
- Clicking a row expands to show full detail (capacity vs. remaining availability, farmer's note)

## Workflow
1. Owner navigates here from Dashboard "Pending Requests" widget or navbar "Booking Requests" link
2. Reviews a pending request against remaining capacity
3. Accepts or Rejects via confirmation modal
4. Request moves to corresponding tab; godown's available capacity updates accordingly

## Interactions
- Same inline Accept/Reject + confirmation modal pattern as Equipment Rental Requests page
- Row shows a small capacity indicator (e.g. "200/500 kg requested — fits available space") to help quick decisions
- Empty state per tab, same style as Equipment Rental Requests

## Design Notes
- Structurally identical to Equipment Rental Requests page — same list/tab/modal pattern, different data fields — for consistency across both owner request flows
- Mobile: stacked cards with full-width Accept/Reject buttons
