# Page: Godown Detail & Booking (Farmer) - Part 2: Workflow & Interactions

## Workflow
1. Farmer lands here from Godown Browse
2. Reviews details and available capacity
3. Enters required capacity and duration in the booking form
4. Clicks "Book Storage" → confirmation modal → redirected to Booking History or stays with "Booking Requested ✓" state

## Interactions
- Capacity input shows a live remaining-capacity indicator (e.g. "480/500 kg available after this booking")
- Form blocks submission if requested capacity exceeds availability, with an inline warning
- Success confirmation modal shows a short summary (godown name, dates, capacity) before closing
