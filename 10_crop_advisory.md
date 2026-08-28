# Page: Crop Advisory (Form & Results)

## Purpose
Let a farmer input their land/season details and receive a rule-based crop recommendation with a cultivation guide.

## Layout — Step 1: Form
- Step-form or single-page form (single page preferred for MVP simplicity): Location, Season (dropdown), Soil type (dropdown, with optional "I don't know" option), Soil pH (optional slider/input), Land size, Previous crop (optional), Irrigation availability (Yes/No toggle)
- "Get Recommendation" button

## Layout — Step 2: Results
- **Recommended crop cards** (2-3 top matches): crop name, image icon, short match reason ("Suits your soil type and season")
- Clicking a card expands/opens a **cultivation guide panel**: growing season, water/soil requirements, growing duration, fertilizer needs, common pests/diseases, precautions
- **Weather-based note strip** at top of results if relevant (e.g. "⚠ Heavy rain expected this week — delay planting sensitive crops")
- "Save this Recommendation" button (ties into farmer's dashboard widget)

## Workflow
1. Farmer arrives from Dashboard "Get Crop Advice" card
2. Fills the form (short, mostly dropdowns to reduce typing)
3. Submits → results screen loads with recommended crops
4. Taps a crop card to expand full cultivation guide
5. Optionally saves the recommendation, which then appears on their Dashboard

## Interactions
- Form uses dropdowns/toggles over free text wherever possible — friendlier for low-literacy users
- Results load with a short "Analyzing your farm details..." loading state to feel purposeful
- Weather warning strip is visually distinct (amber background, icon) but not alarming

## Design Notes
- This page should feel like the "smart" centerpiece of the app — clean, card-based, not a wall of text
- Icons for each crop (simple line icons) help scannability over relying on farmer to read crop names only
