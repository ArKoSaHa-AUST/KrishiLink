# Page: Equipment Browse / Search (Farmer) - Part 2: Workflow & Interactions

## Workflow
1. Farmer arrives from Dashboard "Find Equipment" card or navbar link
2. Applies filters (type, location, dates) — results update live
3. Scans grid of equipment cards
4. Clicks a card or "View Details" → routes to Equipment Detail & Rental Request page

## Interactions
- Filters apply instantly (no separate "Apply" button needed, but a "Clear Filters" link is present)
- Skeleton loading cards while results fetch
- Empty state: "No equipment matches your filters — try widening your search" with a "Clear Filters" button
- Grid becomes single column on mobile, filters collapse into a "Filters" button that opens a bottom sheet
