# Page: Godown Detail & Booking (Farmer)

## Purpose
Show full details of one godown and let the farmer book storage space.

## Layout
- **Image gallery** at top
- **Details panel**: Name, location, total capacity, available capacity, daily/monthly rate, owner name, description/facilities (e.g. "ventilated", "pest-controlled")
- **Booking form**: Duration (start/end date), Capacity needed (input, validated against available capacity), optional note, "Book Storage" button

## Workflow
1. Farmer lands here from Godown Browse
2. Reviews details and available capacity
3. Enters required capacity and duration in the booking form
4. Clicks "Book Storage" → confirmation modal → redirected to Booking History or stays with "Booking Requested ✓" state

## Interactions
- Capacity input shows a live remaining-capacity indicator (e.g. "480/500 kg available after this booking")
- Form blocks submission if requested capacity exceeds availability, with an inline warning
- Success confirmation modal shows a short summary (godown name, dates, capacity) before closing

## Design Notes
- Mirrors the Equipment Detail & Rental Request page layout/pattern for consistency across the two booking flows
- Mobile: sticky bottom "Book This Godown" bar, same as equipment detail page
