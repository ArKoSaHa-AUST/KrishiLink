# Page: Equipment Browse / Search (Farmer)

## Purpose
Let a farmer discover and filter available equipment for rent.

## Layout
- **Filter sidebar (left, collapsible on mobile)**: Equipment type (dropdown/checkboxes: tractor, tiller, harvester, seeder, sprayer), Location, Price range slider, Availability (date picker)
- **Search bar** at top of results area
- **Results grid**: cards showing equipment image, name, type, price (per hour/day), location, availability badge (Available/Unavailable), "View Details" button
- Sort dropdown: Price (low-high), Distance, Newest

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

## Design Notes
- UI Theme: Agriculture-related green and white color palette — crisp white background and card surfaces with vibrant agricultural green highlights, green filter accents, and green primary action buttons
- Equipment images are the primary visual anchor on each card — keep them large and consistent aspect ratio
- Availability badge uses color coding consistent with dashboard status badges
