# Page: Add / Edit Equipment Listing (Equipment Owner) - Part 1: Architecture & Form Layout

## Purpose
Let an equipment owner add a new piece of machinery or edit an existing listing with full pre-filled support.

## Layout & Form Fields
- **Equipment Details**:
  - Equipment Name (text input)
  - Type / Category (dropdown: Tractor, Power Tiller, Combine Harvester, Seed Drill / Seeder, Power Sprayer, Irrigation Pump, Thresher, Other)
  - Description (textarea for specifications, HP, attachments, condition)
  - Location (District / Address input)
- **Pricing Configuration**:
  - Daily Rate (numeric input, required)
  - Hourly Rate (numeric input, optional)
- **Form Actions**:
  - "Save Listing" button (primary action for new listing)
  - "Update Listing" button (primary action when editing existing listing)
  - "Cancel" button (secondary action, returns to Owner Dashboard)
- **Controller & ViewModel**:
  - `EquipmentListingViewModel` in `Models/ViewModels/`
  - Controller actions in `EquipmentOwnerController.cs`:
    - GET: `/EquipmentOwner/Create` (Add mode)
    - GET: `/EquipmentOwner/Edit/{id}` (Edit mode with pre-filled data)
    - POST: `/EquipmentOwner/Save` (Handles create/update submission)
