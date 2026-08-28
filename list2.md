# Page: Add / Edit Equipment Listing (Equipment Owner) - Part 2: Media Upload, Validation & Workflow

## Workflow
1. Owner arrives from Dashboard "Add New Equipment" quick-action card or "Edit" button on an existing listing
2. Fills/edits form fields and configures rates
3. Uploads equipment photos via drag-and-drop or file browser
4. Clicks "Save Listing" / "Update Listing" → success notification → redirected back to Dashboard (`/EquipmentOwner/Index`), new/updated listing visible in "My Listings"

## Interactions & Media
- **Drag-and-Drop Image Uploader**: Tap-friendly dropzone supporting multiple image uploads (PNG, JPG, WebP)
- **Live Thumbnail Previews**: Uploaded images show live thumbnail preview cards with a remove (✕) button per image
- **Inline Validation**:
  - Required fields highlighted with clear error messages
  - Price must be a positive number (> 0)
  - At least one image required before final submission
- **Type Dropdown with Icons**: Quick visual recognition for tractors, tillers, harvesters, seeders, and sprayers
