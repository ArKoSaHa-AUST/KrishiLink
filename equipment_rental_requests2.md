# Page: Equipment Rental Requests (Equipment Owner) - Part 2: Workflow & Interactions

## Workflow
1. Owner navigates here from Dashboard "Pending Requests" or navbar "Requests" link
2. Reviews a pending request's dates and note
3. Clicks Accept or Reject → confirmation modal ("Accept this rental request from [Farmer]?") → confirms
4. Request moves to the corresponding tab, farmer is notified (status updates on their end)

## Interactions
- Accept/Reject buttons show a brief loading state, then the row animates out of the Pending tab
- Rejecting can optionally prompt a short reason field (helps farmer understand, non-mandatory)
- Empty state per tab: "No pending requests right now"
