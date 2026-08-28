# Page: Add / Edit Equipment Listing (Equipment Owner)

## Purpose
Let an equipment owner add a new piece of equipment or edit an existing listing.

## Layout
- Form fields: Equipment Name, Type (dropdown: tractor, tiller, harvester, seeder, sprayer, other), Description (textarea), Images (upload, multiple), Price (hourly/daily toggle + amount), Location
- "Save Listing" button (primary), "Cancel" (secondary, returns to dashboard)
- If editing an existing listing, form is pre-filled and button reads "Update Listing"

## Workflow
1. Owner clicks "Add New Equipment" from Dashboard, or "Edit" on an existing listing
2. Fills/edits form fields
3. Uploads at least one image (drag-and-drop or tap to browse)
4. Clicks Save/Update → success toast → redirected back to Dashboard, new/updated listing visible in "My Listings"

## Interactions
- Image upload shows thumbnail previews with a remove (✕) option per image
- Inline validation (required fields highlighted, price must be a positive number)
- Type dropdown uses icons next to each option for quick recognition

## Design Notes
- Keep form to a single scrollable page rather than multi-step wizard — this is a simple data-entry task
- Mobile: image upload area is large and tap-friendly
