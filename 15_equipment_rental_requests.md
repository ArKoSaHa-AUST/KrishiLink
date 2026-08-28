# Page: Equipment Rental Requests (Equipment Owner)

## Purpose
Full list of incoming rental requests where the owner can review details and Accept/Reject.

## Layout
- **Filter tabs**: Pending | Accepted | Rejected | Completed
- **Request list/table**: farmer name, equipment name, requested dates, note from farmer, status, Accept/Reject buttons (visible only on Pending tab)
- Clicking a row expands to show full request detail (farmer's note, equipment details recap)

## Workflow
1. Owner navigates here from Dashboard "Pending Requests" or navbar "Requests" link
2. Reviews a pending request's dates and note
3. Clicks Accept or Reject → confirmation modal ("Accept this rental request from [Farmer]?") → confirms
4. Request moves to the corresponding tab, farmer is notified (status updates on their end)

## Interactions
- Accept/Reject buttons show a brief loading state, then the row animates out of the Pending tab
- Rejecting can optionally prompt a short reason field (helps farmer understand, non-mandatory)
- Empty state per tab: "No pending requests right now"

## Design Notes
- This page is decision-focused — keep it scannable, avoid unnecessary detail until a row is expanded
- Mobile: rows become stacked cards with Accept/Reject as full-width buttons
