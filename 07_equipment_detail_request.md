# Page: Equipment Detail & Rental Request (Farmer)

## Purpose
Show full details of one piece of equipment and let the farmer submit a rental request.

## Layout
- **Image gallery** at top (main image + thumbnails if multiple images)
- **Details panel**: Name, type, description, hourly/daily rate, owner name & rating (future), location, current availability calendar
- **Request form** (right side on desktop, below details on mobile): Start Date, End Date (or hours), optional note to owner, "Send Rental Request" button
- **Availability calendar**: visually blocks out dates already booked

## Workflow
1. Farmer lands here from Equipment Browse
2. Reviews details and availability calendar
3. Selects date range in the request form
4. Adds optional note
5. Clicks "Send Rental Request" → confirmation modal ("Request sent to owner, you'll be notified once accepted") → redirected to Booking History or stays on page with a "Request Sent" state

## Interactions
- Calendar disables already-booked dates visually (grayed out)
- Form validates that end date is after start date, and dates don't overlap existing bookings
- Button changes to "Request Sent ✓" (disabled) after successful submission, preventing duplicate requests

## Design Notes
- Keep request form always visible/sticky on desktop so farmer doesn't need to scroll back up
- Mobile: form appears as a sticky bottom bar with a "Request This Equipment" button that expands the form
