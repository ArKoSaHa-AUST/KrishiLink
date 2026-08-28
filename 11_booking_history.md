# Page: Booking / Transaction History (Farmer)

## Purpose
Give the farmer a single place to track all past and current equipment rentals and godown bookings.

## Layout
- **Tabs**: All | Equipment Rentals | Godown Bookings
- **Filter row**: Status filter (All / Pending / Accepted / Completed / Rejected), date range
- **List/table of bookings**: item name + image thumbnail, type (Equipment/Godown), dates, price, status badge, "View Details" action
- Clicking a row expands or navigates to a detail view showing the original listing info + request note + status timeline

## Workflow
1. Farmer arrives from Dashboard "Active bookings" summary or navbar link
2. Switches tabs to filter by type
3. Scans list, status badges give instant read on what needs attention (e.g. Pending)
4. Clicks a booking to see full detail/timeline

## Interactions
- Status badges reuse the same color convention from Dashboard (yellow/green/gray/red)
- Table becomes stacked cards on mobile instead of horizontal rows
- Empty state per tab: "No godown bookings yet" with CTA to Godown Browse

## Design Notes
- This page is about clarity and trust — farmers should be able to tell at a glance what's happening with each request
- Keep status timeline simple: Requested → Accepted/Rejected → Completed
