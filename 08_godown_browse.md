# Page: Godown Browse / Search (Farmer)

## Purpose
Let a farmer discover and compare storage (godown) options.

## Layout
- **Filter sidebar**: Location, Capacity needed (slider/input), Price range (daily/monthly), Available date range
- **Search bar** at top
- **Results grid**: cards with godown image, name, location, capacity, price, availability badge, "View Details" button
- Sort dropdown: Price, Distance, Capacity

## Workflow
1. Farmer arrives from Dashboard "Find Storage" card or navbar link
2. Filters by location and required capacity
3. Compares cards in the grid
4. Clicks a card → routes to Godown Detail & Booking page

## Interactions
- Same live-filtering pattern as Equipment Browse for consistency
- Capacity shown clearly (e.g. "500 kg available") so farmers can quickly compare without opening details
- Empty state and loading skeletons mirror Equipment Browse page

## Design Notes
- UI Theme: Agriculture-related green and white color palette — mirrors 06's layout with clean white cards, soft green borders, and nature-green action elements
- Reuse the same card/grid/filter component pattern as Equipment Browse — keeps the app feeling consistent and reduces the learning curve between the two browse flows
