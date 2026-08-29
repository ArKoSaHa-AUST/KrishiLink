# Page: Add / Edit Godown Listing (Godown Owner)

## Purpose
Let a godown owner add a new storage listing or edit an existing one.

## Layout
- Form fields: Godown Name, Location, Total Capacity (with unit, e.g. kg/ton), Description/Facilities (textarea — e.g. ventilated, pest-controlled, cold storage), Images (upload, multiple), Price (daily/monthly toggle + amount)
- "Save Listing" / "Update Listing" button, "Cancel" secondary link

## Workflow
1. Owner clicks "Add New Godown" from Dashboard, or "Edit" on an existing listing
2. Fills/edits form fields including total capacity
3. Uploads images
4. Saves → success toast → redirected to Dashboard, listing visible in "My Godowns"

## Interactions
- Same image upload pattern (thumbnails + remove) as Equipment Listing Form for consistency
- Capacity field validated as a positive number with unit selector
- Inline validation for required fields

## Design Notes
- UI Theme: Agriculture-related green and white color palette — clean white form container with green input focus borders, agricultural green dropzone highlight, and prominent green save button
- Structurally mirrors Equipment Listing Form — same component patterns, different field set — to minimize design/dev duplication and keep the app feeling cohesive
