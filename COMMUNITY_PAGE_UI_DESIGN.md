# KrishiLink Community Hub (Krishi Community)
## Complete UI/UX Design, Functional Specification, and Feature Extension Blueprint

Document Version: 2.0.0  
Status: Comprehensive Specification and Architecture Document  
Target Platform: ASP.NET Core MVC, Bootstrap 5.3, KrishiLink Enterprise Design System, Vanilla JavaScript  
Target Audience: Bangladeshi Farmers, Agricultural Equipment Owners, Godown Owners, Department of Agricultural Extension (DAE) Officers, Agronomists, Community Leaders, Platform Administrators  
Visual Standard: Clean Enterprise Design (Strictly zero emojis and zero decorative unicode signs; all UI states rendered using typography, structural boundaries, and semantic iconography)

---

## Table of Contents
1. Executive Architectural Blueprint and Mission Framework
2. Topbar Global Navigation Integration
3. Information Architecture and Global Page Layout
4. Left Sidebar: Identity Summary, Navigation Trees, and Regional Scoping
5. Central Feed: The Interactive Post Composer
6. Central Feed: Stream Controller, Filtering, and Sorting Mechanics
7. Central Feed: Post Card Component Hierarchy and Interaction States
8. Dedicated Sub-System: "Help Needed" (Krishi Problem and Diagnostic Workflow)
9. Dedicated Sub-System: Audio Voice Note Suite (Rural Inclusivity Engine)
10. Right Sidebar: Auxiliary Intelligence, Emergency Dispatch, and Live Widgets
11. Comprehensive Feature Extensions and Advanced Modules
12. Mobile-First Responsive UI and Touch Interaction Blueprint
13. Moderation, Content Safety, and Governance Framework
14. Visual Design System, Design Tokens, and Component Styling
15. Complete UI Text Strings and Localization Dictionary (Bengali and English)
16. Detailed User Journeys and End-to-End Task Scenarios

---

## 1. Executive Architectural Blueprint and Mission Framework

### 1.1 Purpose and Strategic Objective
The KrishiLink Community Hub is an integrated agricultural social communication and peer-to-peer advisory platform built directly into the KrishiLink ecosystem. The system bridges the information divide across Bangladesh's agricultural value chain by connecting primary producers (farmers), asset owners (machinery and godown operators), and certified agricultural experts (DAE officers, agronomists, and university extension specialists).

While traditional social media platforms introduce high distraction, unstructured data, and algorithmic noise, the KrishiLink Community Hub is structured around agronomic taxonomy, geographic proximity, crop seasonal cycles, and urgent problem-solving protocols.

### 1.2 Rural Bangladesh Usability Context and Demographics
The interface is engineered to address the specific socio-technical realities of rural Bangladesh:
1. Device Constraints: Over 85 percent of users access the system via low-cost Android smartphones with limited RAM (2GB to 4GB) and modest screen resolutions (HD and HD+).
2. Connectivity Variances: Rural cellular networks fluctuate frequently between 3G and 4G with latency spikes and intermittent packet drops.
3. Literacy and Language Variances: While many farmers can navigate basic smartphone interfaces, text typing in Bengali via complex digital keyboards is often slow and prone to typographical errors. Spoken communication remains the primary medium of peer knowledge exchange.
4. Outdoor Environmental Factors: Smartphone screens are frequently viewed under intense direct sunlight in crop fields, requiring high contrast ratios, distinct borders, and prominent touch targets (minimum 44 by 44 pixels).

### 1.3 Strict Zero-Emoji Design Standard
In compliance with enterprise UI guidelines, all decorative unicode emojis, pictograms, and visual signs are excluded from the user interface, labels, buttons, headers, and specifications. Visual identity is maintained exclusively through clean structural layouts, clear typography, professional geometric badges, standardized Bootstrap icons (such as bi-people-fill, bi-geo-alt, bi-check-circle), and intentional color contrast tokens.

### 1.4 Bilingual Architectural Consistency
The Community Hub natively adheres to the platform-wide localization standard. All labels, placeholder texts, validation notices, status pills, and system messages are fully mapped between English (en) and Bengali (bn), respecting grammatical conventions and culturally recognized agricultural terminology.

---

## 2. Topbar Global Navigation Integration

### 2.1 Spatial Placement in Layout Header
The Community entry point is positioned on the primary sticky navigation bar within the desktop header and the mobile offcanvas navigation drawer.

On desktop viewports, the Community button is placed immediately to the right of the Leaderboard (লিডারবোর্ড) navigation link and to the left of the Language Switcher and User Profile controls.

```
+---------------------------------------------------------------------------------------------------------+
| [Brand: KrishiLink]   Home   Equipment   Godowns   Crop Advisory   Leaderboard   [COMMUNITY]  | [EN|BN] [User ▾] |
+---------------------------------------------------------------------------------------------------------+
                                                                                  ^
                                                                        Primary Nav Placement
```

### 2.2 Desktop Navigation Item Specifications
1. Component Hierarchy:
   - Navigation Link Container: Unordered list item within navbar-nav.
   - Anchor Target: Route targeting Controller "Community", Action "Index".
   - Icon Graphic: Bootstrap Icon "bi-people-fill".
   - Text Label: Displaying localized string "Community" (English) or "কমিউনিটি" (Bengali).
   - Dynamic Notification Counter: A small, high-contrast numeric badge indicating unresolved urgent crisis posts published within the user's registered home district in the past 24 hours.
2. Interaction States:
   - Default State: Transparent background, white text (#ffffff) with 85 percent opacity, font-weight 500, font-size 15px.
   - Hover State: Background tint with 15 percent white overlay, 100 percent white text opacity, 200ms ease-in-out transition.
   - Active State (When user is on Community pages): Solid subtle background highlight (#3b8865), 100 percent white text opacity, bottom accent border (3px solid #52b788), font-weight 600.
   - Focus State: Visible outline ring (2px solid #b7e4c7) with 2px offset for keyboard accessibility.

### 2.3 Mobile Offcanvas Navigation Drawer Specifications
Within the mobile navigation drawer (sliding in from the left or right on mobile devices):
1. Element Structure:
   - Block-level list item with full-width clickable touch area (height: 52px).
   - Left-aligned icon wrapper: 36 by 36 pixel rounded container with light forest green background (#eef7f2) and deep green icon (#2d6a4f).
   - Label and Sub-label Stack:
     - Primary Label: "কমিউনিটি" (Community) in 16px semi-bold typography.
     - Secondary Context Description: "কৃষক অভিজ্ঞতা, পরামর্শ ও সমস্যা সমাধান" (Farmer experiences, advice, and problem solving) in 12px muted text (#5e6e61).
   - Right-aligned counter pill: Displaying the number of active discussions in the user's upazila.
2. Touch Target Metrics:
   - Minimum tap boundary: 48px height by 100 percent width.
   - Ripple or background-tint feedback upon touch initiation.

---

## 3. Information Architecture and Global Page Layout

### 3.1 Three-Column Ergonomic Layout (Desktop Viewports >= 1200px)
The Community page utilizes an asymmetric three-column grid system optimized for visual focus, information density, and low distraction:

```
+---------------------------------------------------------------------------------------------------+
|                                  KRISHILINK GLOBAL TOPBAR                                         |
+---------------------------------------------------------------------------------------------------+
|  LEFT SIDEBAR (280px)      |       CENTRAL FEED STREAM (Max 680px)       | RIGHT SIDEBAR (320px)  |
|  - Sticky Container        |       - Dynamic Fluid Column                | - Sticky Container     |
|                            |                                             |                        |
|  [User Identity Profile]   |  [Interactive Post Composer]                | [Urgent Dispatch Board]|
|  - Avatar and Role Badge   |  - Experience, Help Needed, Tip Tabs        | - Real-time local crisis|
|  - District and Upazila    |  - Textarea, Image, Video, Voice Upload     | - Jump to solution     |
|  - Community Reputation    |                                             |                        |
|                            |  [Stream Controller and Filter Bar]         | [Agro-Weather Advisor] |
|  [Navigation Tree]         |  - Filter Tabs: All, Help Needed, Solved    | - Spray suitability    |
|  - All Discussions         |  - Sorting Dropdown: Latest, Trending       | - 3-day local outlook  |
|  - Help Needed Category    |                                             |                        |
|  - Success Stories         |  [Post Stream: Post Card 1]                 | [Top Mentors Board]    |
|  - Machinery and Tech      |  - Author Header and Verification Badge     | - Monthly top solvers  |
|  - Storage and Godown      |  - Urgent Context Banner (if Help Needed)   | - Karma point counts   |
|  - Local Market Prices     |  - High-Resolution Media / Audio Player     |                        |
|                            |  - Accepted Solution Container (if Solved)  | [Emergency Helplines]  |
|  [Geographic Filter]       |  - Reaction Bar and Comment Thread          | - 16123 Krishi Call    |
|  - District Selector       |                                             | - 1090 Disaster Warning|
|  - Scope: All vs Local     |  [Post Stream: Post Card 2...]              | - 333 National Info    |
+---------------------------------------------------------------------------------------------------+
```

### 3.2 Viewport Responsive Breakpoint Rules
1. Desktop Viewport (1200px and above):
   - Left Sidebar: 280px fixed width, sticky position (top: 80px).
   - Central Feed Column: Flex-grow 1, maximum width bounded at 680px, centered.
   - Right Sidebar: 320px fixed width, sticky position (top: 80px).
2. Laptop / Medium Viewport (992px to 1199px):
   - Left Sidebar: 240px fixed width.
   - Central Feed Column: Flex-grow 1.
   - Right Sidebar: Hidden by default; accessible via a secondary toggle button or docked beneath the feed.
3. Tablet Viewport (768px to 991px):
   - Left Sidebar: Hidden from main grid; accessible via offcanvas slide-over drawer triggered by a persistent filter button.
   - Central Feed Column: 100 percent width with 16px lateral padding.
   - Right Sidebar: Converted into collapsible horizontal accordion cards placed below primary content or inside tabbed drawers.
4. Mobile Viewport (Below 768px):
   - Single-column layout.
   - Sticky category filter carousel beneath topbar.
   - Full-width post cards with edge-to-edge media rendering.
   - Floating Action Button (FAB) anchored at bottom-right for post creation.

---

## 4. Left Sidebar: Identity Summary, Navigation Trees, and Regional Scoping

### 4.1 User Identity Profile Summary Card
Positioned at the apex of the left sidebar, this component reinforces the user's community identity and trust credentials:
1. Visual Elements:
   - User Avatar: 56 by 56 pixel circular container with 2px solid border in muted leaf green (#b7e4c7). Displays user profile photo or generated initials fallback on neutral grey background.
   - Online Status Indicator: 10 by 10 pixel solid green dot (#2d6a4f) positioned at the bottom right corner of the avatar.
   - Full Name: Displayed in 16px bold typography (#1b2e21).
   - Role and Verification Pill:
     - Verified Farmer: "ভেরিফাইড কৃষক" (Verified Farmer) in subtle green pill (#eef7f2 background, #1b4332 text, 1px solid #b7e4c7 border).
     - Equipment Owner: "যন্ত্রপাতি মালিক" (Equipment Owner) in subtle blue pill (#e7f1ff background, #0d47a1 text).
     - Godown Owner: "গুদাম মালিক" (Godown Owner) in subtle amber pill (#fff8e6 background, #8a5300 text).
     - Agricultural Extension Officer: "কৃষি কর্মকর্তা (DAE)" in purple pill (#f3e8ff background, #6b21a8 text).
   - Geographic Tag: Location icon followed by Upazila and District name (e.g., "দিনাজপুর সদর, দিনাজপুর").
   - Community Reputation Metric: Horizontal split showing:
     - Krishi Points Balance: "১,৪২০ পয়েন্ট" (1,420 Points) with direct link to Leaderboard.
     - Accepted Solutions Count: "১২টি সমাধান গৃহীত" (12 Solutions Accepted).

### 4.2 Primary Feed Category Navigation Tree
A vertical list of navigational filter links allowing users to segment the feed stream by agricultural intent:
1. Category Items:
   - All Discussions (সব আলোচনা ও আপডেট): Aggregate stream of all verified posts.
   - Help Needed / Disease and Crisis (জরুরি সাহায্য ও রোগবালাই): Restricted to open problem posts with high visibility for unsolved cases. Features a high-contrast count badge.
   - Farmer Experiences and Success Stories (কৃষক অভিজ্ঞতা ও সাফল্য): Dedicated to bumper harvests, crop management techniques, and yield reports.
   - Agricultural Machinery and Technology (যন্ত্রপাতি ও আধুনিক চাষাবাদ): Tractor operations, combine harvester reviews, irrigation pump diagnostics, and rental experiences.
   - Godown and Post-Harvest Preservation (গুদাম সংরক্ষণ ও ব্যবস্থাপনা): Grain drying, moisture control, cold storage experiences, and warehouse availability.
   - Local Market Rates and Trade (স্থানীয় হাট ও বাজার দর): Real-time wholesale prices from regional rural bazars and trading hubs.
   - Fertilizer, Seeds, and Pest Management (সার, বীজ ও কীটনাশক তথ্য): Discussion on seed germination rates, balanced fertilizer application (NPK), and organic pest controls.
2. Visual States of Category Links:
   - Default: Height 42px, padding 8px 12px, border-radius 6px, transparent background, text color #1b2e21, font-weight 500.
   - Hover: Background tint #eef7f2, text color #1b4332, smooth transition.
   - Active: Background solid #2d6a4f, text color #ffffff, font-weight 600, right-aligned count badge rendered with white text on dark green accent (#1b4332).

### 4.3 Regional Geographic Scoping Control
Enables farmers to restrict the community feed to hyper-local concerns or broaden it nationwide:
1. Control Interface:
   - Segmented Toggle: Two mutually exclusive buttons:
     - "সমগ্র বাংলাদেশ" (All Bangladesh): Global nationwide feed.
     - "আমার জেলা" (My District): Automatically filtered to the user's home district (e.g., Bogura).
   - District Dropdown Selector: Allows manual exploration of any of the 64 districts of Bangladesh grouped under the 8 administrative divisions (Dhaka, Chattogram, Rajshahi, Rangpur, Khulna, Barishal, Sylhet, Mymensingh).
   - Radius Proximity Selector (Optional): Radial filter (within 10 km, 25 km, 50 km) based on Upazila GPS coordinates.

### 4.4 Personal Activity and Quick Bookmarks
A secondary navigation block providing instant access to the user's personal interactions:
1. "আমার পোস্টসমূহ" (My Posts): Posts authored by the authenticated user with status counters (Pending, Solved, Active).
2. "আমার দেওয়া সমাধান" (My Answers): Answers and comments submitted by the user on other farmers' help-needed queries.
3. "সংরক্ষিত পোস্ট" (Saved Bookmarks): Posts bookmarked by the user for subsequent technical reference (e.g., specific pesticide formulation advice).

---

## 5. Central Feed: The Interactive Post Composer

### 5.1 Collapsed State (Default Embedded View)
Mounted at the top of the central feed, the collapsed composer mimics familiar social paradigms while encouraging structured agronomic posting:
1. Layout Structure:
   - White surface card (#ffffff), subtle border (1px solid #e2e8df), border-radius 10px, box-shadow (0 2px 6px rgba(45, 106, 79, 0.05)).
   - Top Section: Circular user avatar (40px) paired with an input trigger button styled like an inactive text input box.
   - Placeholder Text: Localized dynamic prompt (e.g., "করিম ভাই, আজ আপনার ফসলের কোনো সমস্যা বা অভিজ্ঞতা আছে? এখানে লিখুন..." / "Karim, do you have any crop updates or questions today? Write here...").
   - Bottom Section: A horizontal row of three prominent quick-action trigger buttons separated by thin vertical dividers:
     - Button 1: "ছবি / ভিডিও" (Photo / Video) with camera icon.
     - Button 2: "সাহায্য চান" (Ask for Help) with life-preserver or help icon.
     - Button 3: "ভয়েস রেকর্ড" (Voice Note) with microphone icon.
2. Trigger Behavior: Clicking any section of the collapsed card instantly triggers the Expanded Composer Modal with the corresponding tab pre-selected.

### 5.2 Expanded Composer Modal Architecture
A dedicated, accessible dialog overlay with full keyboard trap, aria-modal attributes, and esc-key cancellation.

```
+---------------------------------------------------------------------------------------------------+
| Create New Community Post                                                                     [X] |
+---------------------------------------------------------------------------------------------------+
| [User Avatar]  Karim Uddin                                                                        |
|                [Audience: Public (All Farmers) ▾]  [Location: Dinajpur Sadar, Dinajpur]          |
|---------------------------------------------------------------------------------------------------|
| SELECT POST TYPE:                                                                                 |
| [X] General Experience and Story   [ ] Ask for Help (Problem)   [ ] Modern Farming Tip            |
|---------------------------------------------------------------------------------------------------|
| (IF 'ASK FOR HELP' IS SELECTED, DEDICATED CONTEXT TRIAGE ENGINE APPEARS HERE)                     |
| Crop Name: [ Aman Rice ▾ ]               Crop Stage / Age: [ 45 Days ]                            |
| Issue Type: [ Pest Attack ▾ ]            Affected Land Area: [ 2 Bigha ]                          |
| Urgency Level: ( ) Normal   ( ) Moderate   (X) High (Crop in danger)                              |
|---------------------------------------------------------------------------------------------------|
| POST CONTENT TEXTAREA:                                                                            |
| Write details about your crop, problem, or farming experience...                                  |
| Use tags like #RiceBlast #PotatoRot #OrganicFertilizer                                            |
|                                                                                                   |
|---------------------------------------------------------------------------------------------------|
| ATTACHMENT PREVIEW TRAY:                                                                          |
| [Image 1: leaf-spot.jpg] [Remove]  [Image 2: field.jpg] [Remove]                                  |
| [Voice Recording Widget: Play/Pause | Duration 0:45 | Delete Recording]                           |
|---------------------------------------------------------------------------------------------------|
| ATTACHMENT CONTROLS:                                                                              |
| [Upload Photos (Max 5)]  [Upload Video (Max 60s)]  [Record Audio Note]  [Tag Location]            |
|---------------------------------------------------------------------------------------------------|
| [ Cancel ]                                                    [ PUBLISH POST (পোস্ট করুন) ]       |
+---------------------------------------------------------------------------------------------------+
```

### 5.3 Detailed Component Fields of the Composer

#### 5.3.1 Post Type Classification Selector
Three distinct segmented pill buttons at the head of the composer:
1. "অভিজ্ঞতা ও গল্প" (General Experience): For general harvest reporting, machinery usage, seasonal updates, and community commentary.
2. "সাহায্য প্রয়োজন" (Help Needed): Activates the Agricultural Crisis Context Engine. Flags the post internally as IsHelpRequest = true and mandates structured fields.
3. "আধুনিক টিপস" (Agritech Tip): For sharing proven farming techniques, homemade organic pesticides, water conservation strategies, and storage management practices.

#### 5.3.2 Agricultural Crisis Context Engine (For Help-Needed Posts)
When "সাহায্য প্রয়োজন" is selected, the modal dynamically unfolds an additional structured input section:
1. Crop Name Dropdown: Pre-populated with major Bangladeshi crop varieties:
   - Cereal Crops: আমন ধান (Aman Rice), বোরো ধান (Boro Rice), আউশ ধান (Aush Rice), গম (Wheat), ভুট্টা (Maize).
   - Cash and Tuber Crops: গোল আলু (Potato), মিষ্টি আলু (Sweet Potato), পাট (Jute), আখ (Sugarcane).
   - Vegetables: বেগুন (Brinjal), টমেটো (Tomato), মরিচ (Chilli), ফুলকপি ও বাঁধাকপি (Cauliflower and Cabbage), শসা ও পটল (Cucumber and Pointed Gourd), পেঁয়াজ ও রসুন (Onion and Garlic).
   - Fruits: আম (Mango), পেয়ারা (Guava), কলা (Banana), ড্রাগন ফল (Dragon Fruit), তরমুজ (Watermelon).
   - Spices and Oilseeds: সরিষা (Mustard), তিল (Sesame), আদা ও হলুদ (Ginger and Turmeric).
   - Custom Write-in: "অন্যান্য ফসল" (Other Crop) with manual text entry.
2. Problem Category Dropdown:
   - রোগবালাই ও ছত্রাক (Fungal and Bacterial Diseases: Leaf spot, blight, rot, wilt).
   - ক্ষতিকর কীটপতঙ্গ (Insect Pest Attack: Stem borer, aphids, whiteflies, brown planthopper).
   - পুষ্টি ও সারের ঘাটতি (Nutrient Deficiency: Nitrogen chlorosis, zinc deficiency, potassium deficiency).
   - সেচ ও জলাবদ্ধতা (Irrigation and Waterlogging Issues).
   - মাটি ও লবণাক্ততা (Soil and Salinity Problems: Coastal salinity, high alkalinity).
   - যন্ত্রপাতি ত্রুটি ও মেরামত (Machinery Breakdown: Tractor PTO fault, harvester clogging).
   - গুদাম ও সংরক্ষণ সংকট (Storage Deterioration: Grain weevil, moisture build-up).
3. Urgency Level Radio Group:
   - "সাধারণ পরামর্শ" (Normal / Consultative): Non-critical inquiries where response time of 24 to 48 hours is acceptable.
   - "মাঝারি জরুরি" (Moderate Urgency): Spreading symptoms requiring intervention within 12 to 24 hours.
   - "অত্যন্ত জরুরি - ফসল নষ্ট হচ্ছে" (High Urgency - Crop in Danger): Rapidly advancing pest outbreak, sudden wilting, or disaster damage requiring same-day emergency peer advice.
4. Crop Stage and Affected Area Inputs:
   - Crop Age / Growth Stage: Numeric input with unit dropdown (দিন / Days, সপ্তাহ / Weeks, চারা অবস্থা / Seedling, ফুল আসা অবস্থা / Flowering, পরিপক্ব অবস্থা / Maturation).
   - Affected Land Area: Numeric input with unit dropdown (শতক / Decimal, বিঘা / Bigha, একর / Acre).

#### 5.3.3 Rich Multilingual Textarea
1. Input Features:
   - Full support for Unicode Bengali typing, phonetic Avro layouts, Bijoy keyboards, and standard English.
   - Auto-growing height (minimum 120px, maximum 320px with internal vertical scroll).
   - Real-time character counter (maximum 3,000 characters).
   - Hashtag parsing engine: Words prefixed with "#" (e.g., #ধানের_ব্লাস্ট, #PotatoLateBlight) are detected in real-time, highlighted with bold blue text, and indexed for subsequent search retrieval.

#### 5.3.4 Media Attachment Pipeline
1. Multi-Photo Uploader:
   - Supports up to 5 images per post.
   - Permitted formats: JPEG, PNG, WebP. Maximum file size: 8MB per uncompressed image.
   - Automatic client-side canvas downscaling: Images exceeding 1920px in width or height are resized client-side to 1280px at 80 percent JPEG quality prior to transmission, saving rural cellular data.
   - Image Preview Strip: Displays thumbnails with an absolute-positioned top-right delete button (bin icon) on each item.
2. Short Video Uploader:
   - Supports 1 video file per post.
   - Permitted formats: MP4, WebM. Maximum duration: 60 seconds. Maximum file size: 25MB.
   - Client-side duration verification using HTML5 video element metadata probe.
   - Video thumbnail generated automatically from the 1-second mark for feed display.
3. Voice Note Recording Attachment:
   - Directly integrated audio recorder (detailed in Section 9).

#### 5.3.5 Location Tagging Control
- Default: Automatically inherits the user's verified home district and upazila from their profile.
- Override: Allows user to click "অবস্থান পরিবর্তন" (Change Location) to specify a distinct field location if posting while traveling.

#### 5.3.6 Publishing and Validation States
1. Validation Rules:
   - A post must contain either non-empty text (minimum 10 characters) OR at least one valid media attachment (image, video, or voice note).
   - For posts flagged as "সাহায্য প্রয়োজন" (Help Needed), selection of Crop Name, Issue Category, and Urgency Level is strictly mandatory.
2. Submission Feedback:
   - When the user clicks "পোস্ট প্রকাশ করুন" (Publish Post), the submit button enters an asynchronous loading state: disabled attribute applied, text replaced by a spinning loader graphic and label "পোস্ট আপলোড হচ্ছে..." (Uploading post...).
   - A lateral progress bar displays file upload progress percentage for heavy media payloads.
   - Upon successful server response (HTTP 200/201), the modal smoothly dismisses, the composer resets, and the freshly created post is prepended to the apex of the central feed with a subtle green flash animation.

---

## 6. Central Feed: Stream Controller, Filtering, and Sorting Mechanics

The stream controller toolbar sits between the composer and the post stream, providing immediate control over the active view.

### 6.1 Primary Feed View Tabs
A horizontal segmented button group with clear active boundaries:
1. "সব আলোচনা" (All Posts): Complete chronological and engagement-weighted stream.
2. "জরুরি সাহায্য ও সমস্যা" (Help Needed): Restricts the feed to open crisis queries. Shows an unread/open badge count.
3. "সাফল্যের গল্প ও অভিজ্ঞতা" (Experiences): Filters exclusively for harvest reports, practical farm innovations, and positive farming outcomes.
4. "আধুনিক টিপস" (Agritech Tips): Curated technical guidance, fertilizer schedules, and water-saving practices.
5. "সমাধানকৃত আর্কাইভ" (Solved Archive): A dedicated repository of historical help posts that have been resolved and verified, functioning as a practical agrarian encyclopedia.

### 6.2 Sorting Engine Dropdown
Allows users to re-order the stream based on current context:
1. "সর্বশেষ পোস্ট" (Latest First - Default): Strict chronological ordering based on CreatedAt timestamp.
2. "জনপ্রিয় ও আলোচিত" (Most Discussed): Ranked by engagement score (calculated from comment count, reaction count, and share count).
3. "সবচেয়ে জরুরি সমস্যা" (Most Urgent): Prioritizes unresolved Help Needed posts marked with High Urgency, sorting by age so critical dying crops receive immediate community eyes.
4. "নিকটবর্তী এলাকার পোস্ট" (Nearest Proximity): Prioritizes posts from the user's home upazila, followed by neighboring upazilas within the same district.

### 6.3 Active Filter Indicator Strip
When any non-default filter is active (such as a specific district or crop type), a persistent dismissible ribbon appears:
- Example: "ফিল্টার সক্রিয়: জেলা - দিনাজপুর | ফসল - আমন ধান [ফিল্টার মুছুন / Clear Filter]"
- Clicking "ফিল্টার মুছুন" resets the feed to standard nationwide view without reloading the entire web page.

---

## 7. Central Feed: Post Card Component Hierarchy and Interaction States

Every item in the central stream is rendered inside a self-contained, modular card component.

### 7.1 Structural Anatomy of a Post Card
```
+---------------------------------------------------------------------------------------------------+
| [Author Avatar]  Author Full Name  [Verified Role Badge]                                    [···] |
|                  Upazila, District • 2 hours ago • Public Scope                                   |
|---------------------------------------------------------------------------------------------------|
| (OPTIONAL URGENT PROBLEM BANNER - ONLY DISPLAYED IF POST IS A HELP REQUEST)                       |
| [Status Badge: Seeking Solution]  Crop: Rice  |  Problem: Blast  |  Urgency: High                 |
|---------------------------------------------------------------------------------------------------|
| Post Text Body: Multilingual formatted content with clickable hashtags...                         |
| "Leaves are showing diamond-shaped brown lesions with grey centers..."                            |
| [See More / বিস্তারিত দেখুন...] (If content exceeds 4 lines)                                       |
|---------------------------------------------------------------------------------------------------|
| Embedded Audio Player (If voice note attached)                                                    |
| [ Play/Pause Button ] [ Audio Waveform Progress Bar ] [ Duration: 0:48 ]                          |
|---------------------------------------------------------------------------------------------------|
| Media Layout Container (1 to 5 images or 1 video)                                                 |
| [ High-resolution edge-to-edge or grid presentation with zoom modal trigger ]                     |
|---------------------------------------------------------------------------------------------------|
| (OPTIONAL ACCEPTED SOLUTION CONTAINER - ONLY DISPLAYED IF A SOLUTION HAS BEEN ACCEPTED)          |
| [Accepted Solution Header: Author-Verified Solution]                                             |
| Solver: Dr. Monjurul Islam [DAE Agronomist]                                                       |
| Solution Text: "Apply Tricyclazole 75 WP at 0.75g per liter of water immediately in late afternoon"|
|---------------------------------------------------------------------------------------------------|
| Social Engagement Metric Counters:                                                                |
| 45 Reactions (Helpful, Like, Love)                 18 Comments  •  6 Shares                       |
|---------------------------------------------------------------------------------------------------|
| Action Toolbar:                                                                                   |
| [ Helpful (সহায়ক) ]    [ Like (ভালো লেগেছে) ]    [ Comment (মন্তব্য) ]    [ WhatsApp Share ]     |
|---------------------------------------------------------------------------------------------------|
| Comment Section (Collapsible Thread):                                                             |
| - Top-Level Comments with Author Badges and Timestamps                                            |
| - 1-Level Nested Replies                                                                          |
| - Comment Input Form with Photo and Voice Note Attachment                                         |
+---------------------------------------------------------------------------------------------------+
```

### 7.2 Header Component Specifications
1. Author Avatar:
   - Dimensions: 44 by 44 pixels.
   - Border radius: 50 percent (circular).
   - Clicking avatar or author name navigates to the public Farmer Trust Profile (/FarmerProfile/View/{id}).
2. Author Name:
   - Font: 15px semi-bold (#1b2e21).
3. Role and Verification Badge:
   - Rendered inline immediately following the author's name.
   - Verified Badge: A solid check-circle icon beside the role title.
   - Role Text: "ভেরিফাইড কৃষক" (Green), "কৃষি কর্মকর্তা" (Purple), "যন্ত্রপাতি মালিক" (Blue), "গুদাম মালিক" (Amber).
4. Meta Information Sub-Line:
   - Geographic indicator (e.g., "দিনাজপুর সদর, দিনাজপুর").
   - Bullet separator.
   - Relative timestamp using localized time strings (e.g., "এইমাত্র" / Just now, "১৫ মিনিট আগে" / 15m ago, "৩ ঘণ্টা আগে" / 3h ago, "গতকাল" / Yesterday, "১২ সেপ্টেম্বর" / 12 Sep).
   - Privacy scope icon: Globe icon indicating public visibility.
5. More Options Kebab Menu (Three Dots Button):
   - Positioned at the top-right corner of the card.
   - Dropdown Menu Options:
     - "পোস্ট সংরক্ষণ করুন" (Bookmark Post): Saves to personal reading list.
     - "লিংক কপি করুন" (Copy Post Link): Copies direct permalink to clipboard with toast notification.
     - "পোস্ট সম্পাদনা" (Edit Post): Available exclusively to post author within 60 minutes of publication if no comments exist.
     - "পোস্ট মুছে ফেলুন" (Delete Post): Available to author and platform administrators. Triggers confirmation dialog.
     - "অনুপযুক্ত বা ভুল তথ্য রিপোর্ট করুন" (Report Misinformation / Abuse): Opens moderation reporting dialog.

### 7.3 Problem Context Banner (For Help-Needed Posts)
Positioned between the header and the post text body, this banner provides structured visual context:
1. Urgent Alert Banner Background:
   - High Urgency: Soft light red background (#fff5f5), solid red border (1px solid #f8b4b4).
   - Moderate Urgency: Soft amber background (#fff8e6), solid amber border (1px solid #ffe0b2).
   - Normal Urgency: Soft green background (#eef7f2), solid green border (1px solid #b7e4c7).
2. Status Tag:
   - If Unsolved: "সমাধান খুঁজছেন" (Seeking Solution) with clock or question icon.
   - If Solved: "সমাধান পাওয়া গেছে" (Solution Found) with checkmark icon and emerald green pill.
3. Metadata Ribbon Items:
   - "ফসল: [ফসলের নাম]" (Crop: Crop Name).
   - "সমস্যা: [সমস্যার ধরন]" (Issue: Issue Category).
   - "বয়স: [ফসলের বয়স]" (Crop Age: Age).
   - "আক্রান্ত জমি: [জমির পরিমাণ]" (Affected Area: Area).

### 7.4 Post Text Content Rendering
1. Typographic Treatment:
   - Bengali Font: Hind Siliguri, weight 400, font-size 15px, line-height 1.65.
   - English Font: Inter, weight 400, font-size 15px, line-height 1.6.
   - High-contrast charcoal text (#1b2e21).
2. Content Truncation ("See More"):
   - Posts exceeding 4 lines of rendered text (approximately 300 characters) are gracefully clamped with CSS line-clamp.
   - A prominent button "আরও দেখুন..." (See More...) allows inline expansion without page reload. Once expanded, a "কম দেখুন" (See Less) toggle is provided.
3. Hashtag and URL Rendering:
   - Hashtags are rendered as clickable links in forest green (#2d6a4f, bold). Clicking any hashtag immediately filters the central feed for all posts matching that tag.
   - External URLs are rendered with underline, target="_blank", and rel="noopener noreferrer nofollow" attributes.

### 7.5 Media Grid Presentation Matrix
The card adapts its media display container depending on the quantity and aspect ratio of attached files:
1. Single Image Display:
   - Renders at 100 percent card width. Maximum display height: 440px.
   - Object-fit set to cover with rounded corners (8px).
   - Clicking opens the full-screen Lightbox Inspection Modal with zoom and pan controls.
2. Two Images Display:
   - Rendered side-by-side in a 50/50 horizontal split grid.
   - Height: 280px with a 4px vertical divider gap.
3. Three Images Display:
   - Primary image occupies 60 percent left column width (height: 320px).
   - Secondary and tertiary images are stacked vertically in the right 40 percent column width (height: 158px each with 4px gap).
4. Four Images Display:
   - Balanced 2 by 2 grid. Height: 160px per image with 4px gap.
5. Five Images Display:
   - Top row: 2 images in 50/50 split.
   - Bottom row: 3 images in 33.3/33.3/33.3 split.
6. Embedded Video Player:
   - 16:9 responsive aspect ratio container with black background.
   - Custom overlay play/pause button, progress scrubbing bar, volume toggle, elapsed time indicator, and fullscreen expander.
   - Preload attribute set to "metadata" to conserve rural cellular bandwidth.

### 7.6 Accepted Solution Highlight Container (Pinned Solution)
When a solution has been marked as accepted by the post author or an authorized agronomist, it is prominently pinned immediately below the post media and above the social toolbar:
1. Visual Container:
   - Surface: Light emerald tint (#eef7f2), left accent border (4px solid #2d6a4f), border-radius 8px, padding 16px.
2. Header Strip:
   - Left: Check-circle icon followed by bold text "গৃহীত সঠিক সমাধান" (Accepted Verified Solution).
   - Right: "লেখক কর্তৃক অনুমোদিত" (Approved by Author) in small muted text.
3. Solver Identification:
   - Avatar (32px), Full Name, Role Badge, and timestamp of the accepted comment.
4. Solution Text Body:
   - Full text of the resolving comment detailing the pesticide formulation, dosage, water ratio, or cultural treatment.
5. Solver Karma Reward Notice:
   - Small status line: "এই সঠিক সমাধান প্রদানের জন্য সমাধানদাতা +৫০ কৃষি পয়েন্ট অর্জন করেছেন" (Solver earned +50 Krishi Points for this verified solution).

### 7.7 Engagement Metrics and Reaction Toolbar
1. Metric Counter Bar:
   - Displays real-time counts separated by subtle dots:
     - Left: Aggregate reaction count (e.g., "[Icon: Helpful] করিম ও আরও ৪২ জন").
     - Right: Comment count (e.g., "১৮টি মন্তব্য") and Share count (e.g., "৬টি শেয়ার").
2. Interaction Toolbar Buttons:
   - Button 1: "সহায়ক" (Helpful) / "পছন্দ" (Like): Toggles reaction. Long-press reveals reaction flyout (Helpful, Like, Love, Insightful).
   - Button 2: "মন্তব্য করুন" (Comment): Focuses and expands the comment input box at the card bottom.
   - Button 3: "হোয়াটসঅ্যাপে শেয়ার" (WhatsApp Share): Instant deep-link triggering WhatsApp client with formatted summary text and post permalink.
   - Button 4: "সংরক্ষণ" (Bookmark): Toggles post save state for personal reading list.

### 7.8 Threaded Comments and Discussion Engine
1. Comment Card Anatomy:
   - Author Avatar (32px).
   - Content Container with light grey background (#f8faf7), rounded border (border-radius 10px), padding 10px 14px.
   - Author Name in bold with role badge.
   - Expert Distinction: If the commenter is a verified DAE Officer or Agronomist, the entire comment card features a subtle green border and an "অফিসিয়াল পরামর্শ" (Official Advisory) badge.
   - Comment Text Body: Formatted text supporting image attachment and voice notes.
   - Sub-Action Line: "পছন্দ করুন" (Like), "উত্তর দিন" (Reply), relative timestamp.
2. Author Solution Acceptance Button:
   - On Help-Needed posts where IsSolved is false, the post author sees an exclusive button beneath every top-level comment:
   - Button Label: "এটি সঠিক সমাধান হিসেবে গ্রহণ করুন" (Accept this as the Correct Solution).
   - Clicking triggers confirmation modal and instantly executes the solution pinning workflow.
3. Thread Nesting:
   - Supports 1 level of threaded replies beneath top-level comments to maintain mobile layout sanity.
   - Subsequent replies are visually indented by 24px with a left connecting vertical guide line.
4. Comment Input Form:
   - Circular user avatar (32px) paired with an input container.
   - Textarea with placeholder "একটি সমাধান বা পরামর্শ লিখুন..." (Write a solution or advice...).
   - Attachment buttons: Photo upload icon, voice note record icon, emoji-free submit arrow button.

---

## 8. Dedicated Sub-System: "Help Needed" (Krishi Problem and Diagnostic Workflow)

The "Help Needed" sub-system is the operational core of the Community Hub, transforming casual social discussions into structured agricultural triage and emergency assistance.

### 8.1 Problem Submission Triage Matrix
To prevent vague questions (such as "My plant is sick, what do I do?"), the submission workflow mandates diagnostic parameters:

| Step | Parameter | Mandatory | UI Input Control | Options / Validation Criteria |
| :--- | :--- | :--- | :--- | :--- |
| 1 | Crop Identity | Yes | Dropdown + Search | 23 standardized DAE crops + write-in option |
| 2 | Crop Growth Stage | Yes | Dropdown | Seedling, Vegetative, Flowering, Grain filling, Harvest |
| 3 | Symptom Category | Yes | Multi-select checkboxes | Leaf discoloration, stem bore, root rot, fruit drop, wilting |
| 4 | Affected Acreage | Yes | Number + Unit dropdown | Decimal, Bigha, Acre, Total farm percentage |
| 5 | Disease Urgency | Yes | Radio group | Normal (24-48h), Moderate (12-24h), High / Crisis (Same day) |
| 6 | Detailed Symptoms | Yes | Textarea (Min 20 chars) | Text describing onset, prior sprays, and soil condition |
| 7 | Visual Evidence | Recommended | Camera / File upload | Up to 5 close-up and panoramic field photos |
| 8 | Voice Description | Optional | Audio Record Widget | Spoken description for low-literacy farmers |

### 8.2 Severity Index and Visual Triage Indicators
1. High Urgency (অত্যন্ত জরুরি):
   - Trigger Criteria: Pest epidemic (e.g., Fall Armyworm, Brown Planthopper hopper-burn), rapid fungal blight spreading across multiple acres.
   - Visual Treatment: 2px solid crimson border on post card (#dc3545), pulsing red indicator dot in header, automatic inclusion in the Right Sidebar Emergency Dispatch Board.
2. Moderate Urgency (মাঝারি জরুরি):
   - Trigger Criteria: Localized leaf spots, nutrient chlorosis, minor insect presence without immediate crop mortality risk.
   - Visual Treatment: 1.5px solid amber border (#fd7e14), amber status badge.
3. Normal / Consultative (সাধারণ পরামর্শ):
   - Trigger Criteria: Soil preparation advice, seed variety recommendations, preventive maintenance questions.
   - Visual Treatment: Standard clean card border (#e2e8df), neutral green status badge.

### 8.3 Geographic Crisis Broadcast and Push Notification Engine
When a High Urgency Help post is published:
1. In-App Notification Dispatch: The system dispatches an alert to all registered farmers, agronomists, and equipment owners residing in the same upazila:
   - Notification String: "জরুরি কৃষি সাহায্য: আপনার উপজেলায় [ফসলের নাম] ফসলে [সমস্যার নাম] দেখা দিয়েছে। পরামর্শ দিতে ক্লিক করুন।" (Urgent Farm Help: Crop issue reported in your upazila. Click to assist.)
2. Agronomist Priority Queue: The post is highlighted at the top of the administrative dashboard for verified DAE agricultural extension officers assigned to that agricultural block.

### 8.4 Diagnostic Commenting and Prescription Protocol
Community members and experts submitting comments on Help posts are encouraged to follow a structured prescription format:
1. Diagnosis Confirmation: Stating the recognized scientific/local disease or pest name (e.g., "এটি ধানের বাদামি ঘাসফড়িং বা কারেন্ট পোকার আক্রমণ" / Brown Planthopper).
2. Immediate Remedial Action: Specific chemical or biological formulation, including active ingredient, trade brand, dosage per liter of water, and spraying time (e.g., late afternoon to protect beneficial insects).
3. Cultural Management: Water drainage, withholding urea nitrogen, or clearing field bunds.
4. Cautionary Advice: Personal protective equipment (PPE) recommendations and pre-harvest interval (PHI) warnings.

### 8.5 Solution Verification and Acceptance Ceremony
1. Eligibility: Only the original post author or an authorized DAE Agronomist has permission to execute the "Accept Solution" action.
2. Workflow Execution:
   - User clicks "সঠিক সমাধান হিসেবে গ্রহণ করুন" (Accept as Correct Solution) on the resolving comment.
   - A modal dialog appears: "আপনি কি নিশ্চিত যে এই পরামর্শটি আপনার ফসলের সমস্যার সঠিক সমাধান করেছে?" (Are you sure this advice resolved your crop problem?).
   - Upon confirmation:
     - Post attribute IsSolved is updated to true.
     - AcceptedCommentId is linked to the selected comment.
     - The selected comment is visually highlighted and cloned into the Pinned Accepted Solution container at the post apex.
     - The comment author is credited with +50 Krishi Loyalty Points.
     - An automated notification is sent to the comment author celebrating their verified contribution.

---

## 9. Dedicated Sub-System: Audio Voice Note Suite (Rural Inclusivity Engine)

### 9.1 Agronomic Justification and Inclusivity Goal
In rural Bangladesh, a substantial demographic of senior and smallholder farmers possess deep practical farming knowledge but face severe challenges typing complex Bengali technical terminology on digital touchscreens. The Audio Voice Note Suite removes this barrier completely, allowing any farmer to speak their problem or advice naturally in their regional Bengali dialect.

### 9.2 Voice Note Recording Interface
Embedded seamlessly inside both the Post Composer and the Comment Input Form:
1. Recording Component States:
   - Idle State: A circular microphone button (height: 40px) accompanied by label "ভয়েস রেকর্ড করুন" (Record Voice).
   - Active Recording State:
     - The button transforms into a pulsing red stop square button.
     - A live elapsed recording timer displays time in minutes and seconds (e.g., "রেকর্ড হচ্ছে: 00:34 / 02:00").
     - An animated audio waveform bar visualizes live input volume to reassure the farmer that their microphone is capturing speech.
     - A "বাতিল করুন" (Cancel) button allows discarding the recording without saving.
   - Review State (Post-Recording):
     - The waveform displays the completed audio signature.
     - A circular play/pause button allows listening back to the recording before publishing.
     - Duration label displays total recording length (e.g., "0:48 মিনিট").
     - A bin icon allows deleting and re-recording if the farmer is unsatisfied with the audio clarity.

### 9.3 Technical Specifications for Audio Capture
1. Browser API: HTML5 MediaStream Recording API (navigator.mediaDevices.getUserMedia).
2. Encoding Format: Compressed WebM / OGG container utilizing Opus audio codec.
3. Bitrate and Payload: Optimized at 24 kbps mono audio. A 60-second voice note consumes only approximately 180 KB of bandwidth, ensuring instant uploading even over 2G/EDGE rural cellular networks.
4. Hard Limit: Maximum recording duration capped at 2 minutes per voice note to prevent file bloat.

### 9.4 Audio Playback Player Component (In Feed and Comments)
When rendered in the post stream or comment thread:
1. UI Layout:
   - Compact horizontal capsule container with subtle grey-green background (#f0f5f2), height 48px, rounded corners (24px pill).
   - Left: Circular play/pause toggle button (32px) in solid forest green (#2d6a4f) with white play icon.
   - Center: Interactive scrub bar allowing forward and backward seeking with touch or mouse drag.
   - Right: Current playback position and total duration (e.g., "0:18 / 0:52").
   - Volume / Speed Control: Speed toggle button allowing playback at 1.0x, 1.25x, or 1.5x speed.

---

## 10. Right Sidebar: Auxiliary Intelligence, Emergency Dispatch, and Live Widgets

The right column operates as a real-time agrarian monitoring dashboard providing immediate regional context.

### 10.1 Urgent Regional Emergency Dispatch Board
1. Purpose: Displays real-time critical crisis posts originating from the user's home district requiring urgent community intervention.
2. Layout Elements:
   - Header: High-contrast red accent banner with text "আপনার জেলার জরুরি সমস্যাসমূহ" (Urgent Needs in Your District).
   - Item Card (Up to 3 items):
     - Crop name and problem title in bold 14px typography (e.g., "ধানের ব্লাস্ট রোগ আক্রমণ").
     - Author name and Upazila (e.g., "সোলেমান মিয়া • বগুড়া সদর").
     - Relative time elapsed (e.g., "১৫ মিনিট আগে" / 15m ago).
     - Direct CTA Link: "দ্রুত সমাধান দিন" (Provide Solution Fast) which jumps directly to the post card and opens the comment input box.
   - Footer Link: "সকল জরুরি সমস্যা দেখুন" (View All Urgent Problems).

### 10.2 Localized Weather and Agricultural Spray Advisory Widget
Directly synchronized with KrishiLink's existing Open-Meteo weather service integration:
1. Widget Content:
   - Location Header: Current detected or selected district (e.g., "বগুড়া সদর, রাজশাহী বিভাগ").
   - Temperature and Condition: Large 28px temperature display (e.g., "২৮° সেলসিয়াস"), weather status text (e.g., "আংশিক মেঘলা" / Partly Cloudy), relative humidity percentage ("আর্দ্রতা: ৭৬%").
   - 3-Day Forecast Strip: Miniature daily icons showing expected rain probability for Day 1, Day 2, and Day 3.
2. Agronomic Spray Recommendation Engine:
   - Analyzes real-time precipitation forecast, wind speed, and humidity to generate an explicit spray advisory:
     - Favorable Spray Window: "আজ বিকেলে বালাইনাশক স্প্রে করার জন্য অনুকূল আবহাওয়া। বৃষ্টির সম্ভাবনা নেই এবং বাতাসের গতিবেগ স্বাভাবিক।" (Favorable weather for spraying this afternoon. No rain expected, wind speed normal.)
     - Unfavorable Spray Warning: "সতর্কতা: আগামী ২৪ ঘণ্টার মধ্যে ভারি বৃষ্টির সম্ভাবনা রয়েছে। বালাইনাশক বা সার প্রয়োগ স্থগিত রাখুন, অন্যথায় ধুয়ে নষ্ট হতে পারে।" (Warning: Heavy rain expected in next 24 hours. Postpone pesticide or fertilizer application to avoid wash-off.)

### 10.3 Monthly Community Mentorship Leaderboard ("সেরা কৃষি বন্ধু")
Connects the Community Hub to the platform's gamification and loyalty reward system:
1. Widget Structure:
   - Header: "চলতি মাসের সেরা কৃষি বন্ধু" (Top Mentors of the Month).
   - Top 3 Contributor Profiles:
     - Rank 1: Gold medal indicator, User avatar, Full Name, Role, Community Karma Points earned this month, Solutions Accepted count.
     - Rank 2: Silver medal indicator, Contributor details.
     - Rank 3: Bronze medal indicator, Contributor details.
   - User Rank Summary: Shows the logged-in user's current rank, points, and how many points needed to reach the next tier.
   - Link: "সম্পূর্ণ লিডারবোর্ড দেখুন" (View Full Leaderboard) navigating to /Leaderboard.

### 10.4 Government Agriculture Helplines and Emergency Call Centers
Provides 1-click telephone calling shortcuts for direct farmer support:
1. Direct Dial Buttons (tel: protocols):
   - "কৃষি কল সেন্টার: ১৬১২৩" (Krishi Call Center - 16123): Official Ministry of Agriculture hotline, toll-free from any local mobile operator.
   - "দুর্যোগের আগাম বার্তা: ১০৯০" (Disaster Early Warning - 1090): Cyclone, flood, and weather hazard hotline.
   - "জাতীয় তথ্য ও সেবা: ৩৩৩" (National Information and Citizen Service - 333): Government service information line.
2. UI Styling: Clean pill buttons with telephone handset icon, distinct light background (#f8faf7), and clear bold numbers.

### 10.5 Seasonal Crop Disease Early Warning Bulletin
A dynamic regional alert box populated by platform administrators and regional DAE alerts:
- Example: "চলতি মৌসুমে আলু ফসলে নাবী ধসা (Late Blight) রোগের ঝুঁকি রয়েছে। কুয়াশাচ্ছন্ন আবহাওয়ায় জমি পর্যবেক্ষণ করুন এবং আগাম ম্যানকোজেব স্প্রে করুন।" (Current season risk for Potato Late Blight. Monitor fields during foggy weather and apply preventive Mancozeb.)

---

## 11. Comprehensive Feature Extensions and Advanced Modules

The following 10 advanced modular extensions are designed to expand the KrishiLink Community Hub from an interactive social feed into an all-encompassing agricultural operating platform:

### 11.1 Extension 1: AI-Assisted Crop Disease Image Scanner and Pre-Diagnostic Assistant
1. Feature Concept: When a farmer uploads leaf or fruit symptom photos in the "Help Needed" composer, an integrated computer vision model analyzes the image client-side/server-side to provide instant preliminary diagnostic suggestions before the post is published.
2. UI Workflow:
   - Farmer uploads photo of spotted tomato leaf.
   - An intelligent assistant banner appears inside the composer: "প্রাথমিক বিশ্লেষণ: এটি টমেটোর আর্লি ব্লাইট (Early Blight) হওয়ার সম্ভাবনা ৮৫%। নিচে সম্ভাব্য লক্ষণ ও চিকিৎসা দেওয়া হলো..." (Preliminary Analysis: 85% probability of Tomato Early Blight).
   - The farmer can confirm or adjust the suggested diagnosis. The post is published with the AI tag attached, allowing community human experts to review, confirm, or correct the preliminary finding.
3. Agronomic Safety Guard: A clear disclaimer states: "এটি একটি প্রাথমিক ডিজিটাল অনুমান। চূড়ান্ত ব্যবস্থা গ্রহণের পূর্বে কৃষি কর্মকর্তা বা অভিজ্ঞ কৃষকের পরামর্শ যাচাই করুন।" (Preliminary digital estimate. Verify with an agronomist before applying chemical treatments.)

### 11.2 Extension 2: Community Equipment Group-Buying and Collective Bargaining Hub
1. Feature Concept: Enables neighboring farmers in the same upazila to pool their equipment rental demands to negotiate bulk discounts from registered KrishiLink machinery owners.
2. UI Workflow:
   - A farmer initiates a "যৌথ যন্ত্রপাতি বুকিং" (Group Machinery Booking) post (e.g., "Seeking combine harvester for 50 combined bighas of Aman rice in Dinajpur Sadar during November 10-15").
   - Neighboring farmers click "আমিও যুক্ত হতে চাই" (Join Group) and declare their individual land area.
   - Once a threshold (e.g., 50 bighas) is reached, registered combine harvester owners on KrishiLink submit competitive bids directly in the comment thread.
   - The booking transitions into an official multi-party contract linked to KrishiLink's existing Booking and Ledger settlement services.

### 11.3 Extension 3: Community Godown Shared Booking and Micro-Storage Pooling
1. Feature Concept: Smallholder farmers producing small quantities of grain (e.g., 2 to 5 tons) often cannot afford to lease an entire commercial godown chamber. This extension facilitates micro-storage syndication.
2. UI Workflow:
   - A community post type labeled "গুদাম স্পেস ভাগাভাগি" (Share Godown Space).
   - Smallholders in the same union coordinate their harvest dates to collectively book a 50-ton cold storage unit or grain warehouse.
   - KrishiLink's backend splits the invoice proportionally, generates individual QR storage receipts for each farmer, and tracks individual lot numbers in the godown.

### 11.4 Extension 4: Hyper-Local Agricultural Marketplace and Certified Seed Exchange
1. Feature Concept: A dedicated community sub-feed where farmers can exchange or purchase surplus authentic heirloom seeds, saplings, organic manure, and vermicompost directly from fellow verified growers.
2. UI Workflow:
   - Post Type: "বীজ ও চারা বিনিময়" (Seed and Sapling Exchange).
   - Mandatory Fields: Seed variety (e.g., "বিনা-১৭ ধান বীজ"), germination test percentage (e.g., "অঙ্কুরোদগম ৯০%"), quantity available (e.g., "৪০ কেজি"), and collection location.
   - Quality Safeguard: Buyers can review the seller's farmer trust profile and completed harvest history on KrishiLink prior to initiating contact.

### 11.5 Extension 5: Direct Agronomist Video Consultations and Virtual Field Clinics
1. Feature Concept: When an urgent crisis post cannot be resolved through text or photos alone, verified DAE officers can schedule an instant 1-on-1 virtual video consultation with the affected farmer.
2. UI Workflow:
   - In the Help Needed post, an authorized expert clicks "ভিডিও কলে পরামর্শ দিন" (Initiate Video Clinic).
   - An in-browser WebRTC secure video room link is generated and sent via SMS and in-app notification to the farmer.
   - The farmer switches to their rear smartphone camera to show live crop roots, soil consistency, and insect movement to the agronomist in real-time.

### 11.6 Extension 6: Verified Field Demonstrations and Masterclass Video Reels
1. Feature Concept: A dedicated visual stream for verified progressive farmers ("কৃষি উদ্যোক্তা") to share high-impact, vertical short-form instructional videos (60 seconds) demonstrating practical agricultural hacks.
2. Examples:
   - "কীভাবে সহজে ফেরোমোন ফাঁদ তৈরি করবেন" (How to construct a low-cost pheromone trap).
   - "ধানের জমিতে পার্চিং (ডাল পোঁতা) পদ্ধতির সঠিক নিয়ম" (Proper method of bird perching in paddy fields).
   - "বোরো ধানের চারা রোপণের আদর্শ দূরত্ব" (Ideal spacing for Boro rice seedling transplantation).
3. UI Treatment: Vertical video feed with double-tap like gesture, swipe to next tutorial, and downloadable offline audio notes.

### 11.7 Extension 7: Agricultural Event, Field Day, and Training Noticeboard
1. Feature Concept: Allows DAE extension offices, Bangladesh Agricultural Research Institute (BARI), and Bangladesh Rice Research Institute (BRRI) to announce local training workshops, field demonstration days, and seed distribution fairs.
2. UI Workflow:
   - Calendar badge with date, venue (e.g., "দিনাজপুর কৃষি প্রশিক্ষণ ইনস্টিটিউট মিলনায়তন"), and target participant capacity.
   - One-tap RSVP button "আমি অংশগ্রহণ করব" (I Will Attend).
   - Automated SMS reminder sent 24 hours prior to the event.

### 11.8 Extension 8: Disaster Relief, Flood, and Drought Mutual Aid Coordination Board
1. Feature Concept: In the wake of natural disasters common to Bangladesh (flash floods in Haor regions, coastal tidal surges, North-Bengal droughts), the Community Hub activates an emergency Mutual Aid Board.
2. UI Workflow:
   - Post Types: "জরুরি সহায়তা প্রয়োজন" (Aid Needed: Seedlings, livestock fodder, diesel for pump) vs. "সাহায্য দিতে সক্ষম" (Aid Offered: Surplus rice seedlings, pump sharing).
   - Geographic Heatmap: Visual map of affected unions highlighting distress points and matching them with unaffected nearby farmers possessing surplus resources.

### 11.9 Extension 9: Offline PWA Sync and SMS Fallback Gateway
1. Feature Concept: Guarantees community functionality even during total rural internet blackouts.
2. Technical Mechanics:
   - Progressive Web App (PWA) with Service Worker caching: The user can draft posts, attach photos, and record voice notes while completely offline in the middle of a crop field.
   - Background Sync API: As soon as the smartphone detects cellular connectivity, queued posts and comments are automatically uploaded in the background.
   - SMS Two-Way Gateway: For urgent crop distress where data connectivity is unavailable, farmers can send a free SMS containing basic codes (e.g., "HELP DINAJPUR RICE BLAST") to platform shortcode. The system creates an automated community help post on their behalf.

### 11.10 Extension 10: Krishi Karma Points, Community Tokenomics, and Tangible Rewards
1. Feature Concept: Connects social contributions directly to economic value on KrishiLink.
2. Earning Rules:
   - Publishing an informative farm story: +10 Points.
   - Submitting a helpful comment on a crisis post: +15 Points.
   - Comment marked as Accepted Solution: +50 Points.
   - Maintaining 30-day helpful streak: +100 Points.
3. Redemption Ecosystem:
   - Accumulated Krishi Points can be redeemed for tangible marketplace discounts:
     - 5 percent discount voucher on next combine harvester or tractor rental.
     - 10 percent discount on godown storage booking fees.
     - Free delivery of certified seeds from partner suppliers.
     - Platform verification fee waiver.

---

## 12. Mobile-First Responsive UI and Touch Interaction Blueprint

### 12.1 Viewport Breakpoint Hierarchy
- Mobile Small (360px to 414px): Single column, full-bleed media cards, horizontal scrollable category pills, floating action button.
- Mobile Large / Phablet (415px to 767px): Single column with 12px lateral padding, rounded cards.
- Tablet (768px to 1024px): Two columns (Collapsible left sidebar drawer, expanded central feed, right sidebar moved to bottom).
- Desktop (1025px and above): Full asymmetric three-column layout.

### 12.2 Mobile Navigation and Sticky Quick-Filter Carousel
Mounted directly underneath the mobile topbar, a horizontal touch-scrollable strip of pill buttons allows instant category switching with zero vertical page consumption:
```
+-------------------------------------------------------------------------+
| [ All Posts ]  [ Help Needed (14) ]  [ Experiences ]  [ Tips ]  [ Solved ] |
+-------------------------------------------------------------------------+
  <-------------------------- Touch Scrollable ------------------------->
```

### 12.3 Floating Action Button (FAB) Architecture
On viewports under 768px, a persistent Floating Action Button is anchored at the bottom-right corner of the screen:
1. Physical Specifications:
   - Shape: Circular, 56 by 56 pixels.
   - Elevation: High z-index (1040), box-shadow (0 4px 16px rgba(45, 106, 79, 0.35)).
   - Surface Color: Deep forest green gradient (#2d6a4f to #1b4332).
   - Icon: Plus icon in pure white (24px).
2. Behavior on Tap:
   - Smoothly launches a full-screen mobile composer sheet sliding up from the screen bottom with native-feeling momentum physics.
   - The user can tap "সমস্যা জানান" (Report Problem) or "অভিজ্ঞতা লিখুন" (Write Story) and submit without leaving the mobile stream context.

### 12.4 Touch Gestures and Ergonomic Enhancements
1. Double-Tap to React: Double-tapping any post photo triggers a brief green leaf reaction animation and toggles the "Helpful / Like" status.
2. Swipe to Dismiss Media: Full-screen lightbox photo preview can be dismissed by swiping downward.
3. Pull-to-Refresh: Pulling down from the top of the feed stream triggers an elastic loading indicator and refreshes the post queue with new community updates.
4. Generous Tap Targets: Every interactive element maintains a minimum touch boundary of 44 by 44 pixels, preventing mis-taps by farmers with calloused hands or while working in muddy field conditions.

---

## 13. Moderation, Content Safety, and Governance Framework

To maintain a constructive, reliable, and family-friendly agricultural environment, the Community Hub implements multi-layered governance:

### 13.1 Automated Pre-Publish Keyword and Safety Shield
Before any post or comment is committed to the database, a background text filter inspects the payload:
1. Banned Categories:
   - Political propaganda, partisan agitation, or non-agricultural polemics.
   - Unlicensed, banned, or hazardous chemical pesticides (e.g., DDT, Endosulfan, unauthorized toxic imports).
   - Abusive language, hate speech, religious incitement, or personal harassment.
   - Commercial spam, irrelevant MLM schemes, and gambling promotions.
2. Action on Trigger: Posts containing prohibited keywords are automatically quarantined into a "Pending Review" status, and the user receives a polite notification: "আপনার পোস্টটি পর্যালোচনার জন্য জমা রাখা হয়েছে" (Your post has been held for administrative review).

### 13.2 Peer Community Reporting Mechanism
Every post and comment features a "রিপোর্ট করুন" (Report) option in its action menu:
1. Structured Report Categories:
   - "ভুল বা ক্ষতিকর কৃষি পরামর্শ" (Harmful or Incorrect Farming Advice: Dangerous chemical mixtures).
   - "অনুপযুক্ত বা আপত্তিকর ভাষা" (Inappropriate or Abusive Language).
   - "ব্যবসায়িক স্প্যাম বা বিজ্ঞাপন" (Commercial Spam or Unauthorized Ads).
   - "রাজনৈতিক বা অপ্রাসঙ্গিক পোস্ট" (Political or Irrelevant Topic).
2. Triage Threshold: If any post receives 3 independent community reports, it is automatically hidden from public feeds and dispatched to the Admin Moderation Queue with high priority.

### 13.3 Administrative Moderation Dashboard (`/Admin/CommunityModeration`)
Dedicated portal for platform administrators and chief agronomists:
1. Operational Tools:
   - Audit stream displaying flagged posts with highlighted violation reasons and reporter comments.
   - One-Click Actions:
     - "অনুমোদন করুন" (Approve and Restore).
     - "স্থায়ীভাবে মুছে ফেলুন" (Permanently Delete).
     - "সতর্কতা পাঠান" (Issue User Warning).
     - "পরামর্শ সংশোধন ট্যাগ যোগ করুন" (Attach Expert Warning Label: e.g., "Caution: Chemical dosage reported in this post exceeds safe thresholds").
2. User Penalty Escalation Protocol:
   - 1st Infraction: Formal in-app warning notice.
   - 2nd Infraction: 7-day temporary ban from posting and commenting.
   - 3rd Infraction: Permanent revocation of community privileges and forfeiture of Krishi Karma Points.

---

## 14. Visual Design System, Design Tokens, and Component Styling

The visual language strictly extends KrishiLink's existing design tokens defined in `wwwroot/css/site.css`:

### 14.1 Color Architecture and Palette Tokens
```css
:root {
    /* Primary Agricultural Brand Greens */
    --krishi-primary: #2d6a4f;           /* Forest Green - Primary Buttons, Headers, Active Tabs */
    --krishi-primary-hover: #1b4332;     /* Dark Leaf Green - Hover states */
    --krishi-primary-active: #0d2818;    /* Deepest Forest Green - Active press states */
    --krishi-primary-light: #eef7f2;     /* Soft Leaf Tint - Accepted solution background, hover rows */
    --krishi-primary-subtle: #d8f3dc;    /* Highlight Tint - Active category backgrounds */

    /* Fresh Green Accents */
    --krishi-accent: #52b788;            /* Fresh Spring Green - Verified badges, success icons */
    --krishi-accent-light: #b7e4c7;      /* Muted Green - Subtle card borders */

    /* Neutral Surfaces and Layout */
    --krishi-bg: #f8faf7;                /* Base Page Background - Warm Off-White */
    --krishi-surface: #ffffff;           /* Card and Modal Background - Pure White */
    --krishi-surface-muted: #f0f2f1;     /* Nested comment and widget background */
    --krishi-border-color: #e2e8df;      /* Card and divider borders */

    /* High-Contrast Typographic Neutrals */
    --krishi-text-primary: #1b2e21;      /* Deep Charcoal Green - Post body, headings, author names */
    --krishi-text-muted: #5e6e61;        /* Muted Grey-Green - Timestamps, meta tags, counters */
    --krishi-text-subtle: #8a998e;       /* Disabled labels, placeholder text */

    /* Diagnostic and Crisis Status Colors (Strictly No Emojis - Semantic Color Tokens) */
    --krishi-status-urgent-bg: #fff5f5;  /* High urgency help card background */
    --krishi-status-urgent-text: #9b1c1c;/* High urgency text and badge color */
    --krishi-status-urgent-border: #f8b4b4; /* High urgency card border */

    --krishi-status-moderate-bg: #fff8e6; /* Moderate urgency card background */
    --krishi-status-moderate-text: #8a5300; /* Moderate urgency text */
    --krishi-status-moderate-border: #ffe0b2; /* Moderate urgency border */

    --krishi-status-solved-bg: #eef7f2;  /* Solved status background */
    --krishi-status-solved-text: #1b4332;/* Solved status text */
    --krishi-status-solved-border: #b7e4c7; /* Solved status border */

    /* Component Geometry and Shadows */
    --krishi-radius-sm: 0.375rem;        /* 6px - Small badges, tag pills */
    --krishi-radius-md: 0.6rem;          /* 10px - Post cards, sidebars, composer */
    --krishi-radius-lg: 0.85rem;         /* 14px - Modals, media containers */
    --krishi-radius-full: 9999px;        /* Full pill buttons, circular avatars */

    --krishi-shadow-sm: 0 2px 6px rgba(45, 106, 79, 0.05);
    --krishi-shadow-md: 0 4px 14px rgba(45, 106, 79, 0.08);
    --krishi-shadow-lg: 0 8px 24px rgba(45, 106, 79, 0.12);
}
```

### 14.2 Typographic Hierarchy
1. Font Families:
   - Primary Bengali Typography: 'Hind Siliguri', system-ui, -apple-system, sans-serif.
   - Primary English Typography: 'Inter', system-ui, -apple-system, sans-serif.
2. Type Scale:
   - Page Section Title: 20px, Bold (700), line-height 1.3.
   - Author Name: 15px, Semi-Bold (600), line-height 1.4.
   - Post Body Text: 15px, Regular (400), line-height 1.65.
   - Meta Sub-Lines and Timestamps: 12px, Regular (400), line-height 1.4.
   - Status Badges and Pills: 11px to 12px, Semi-Bold (600), letter-spacing 0.3px.

### 14.3 Standard Iconography (Bootstrap Icons 1.11.3)
All visual actions are mapped to clean semantic Bootstrap icons (zero unicode emojis):
- Community Navigation: `bi-people-fill`
- Verified Status: `bi-check-circle-fill`
- Geographic Pin: `bi-geo-alt-fill`
- Photo Upload: `bi-camera-fill`
- Video Upload: `bi-camera-video-fill`
- Voice Note Record: `bi-mic-fill`
- Play Audio: `bi-play-fill`
- Pause Audio: `bi-pause-fill`
- Help Needed Alert: `bi-exclamation-triangle-fill`
- Solved Status: `bi-patch-check-fill`
- Reaction Like: `bi-hand-thumbs-up-fill`
- Reaction Helpful: `bi-lightbulb-fill`
- Comment: `bi-chat-left-text-fill`
- Share: `bi-share-fill`
- Bookmark: `bi-bookmark-fill`
- More Options: `bi-three-dots`
- Telephone Helpline: `bi-telephone-fill`
- Weather / Sunlight: `bi-sun-fill`
- Rain Forecast: `bi-cloud-rain-fill`

---

## 15. Complete UI Text Strings and Localization Dictionary (Bengali and English)

The following comprehensive dictionary defines all interface strings for the bilingual system:

### 15.1 Navigation and Header Strings
| Key | English Value | Bengali Value (বাংলা) |
| :--- | :--- | :--- |
| `Nav_Community` | Community | কমিউনিটি |
| `Nav_Community_Desc` | Farmer stories, advice & crisis support | কৃষক আলোচনা, অভিজ্ঞতা ও সমস্যা সমাধান |
| `Header_Title` | KrishiLink Community Hub | কৃষিলিঙ্ক কৃষক কমিউনিটি |
| `Header_Subtitle` | Connect, share experiences, and solve crop problems together | সংযুক্ত হোন, অভিজ্ঞতা ভাগ করুন এবং একসাথে ফসলের সমস্যা সমাধান করুন |

### 15.2 Left Sidebar and Navigation Tree Strings
| Key | English Value | Bengali Value (বাংলা) |
| :--- | :--- | :--- |
| `Sidebar_Category_All` | All Discussions | সব আলোচনা ও আপডেট |
| `Sidebar_Category_Help` | Help Needed (Problems) | জরুরি সাহায্য ও রোগবালাই |
| `Sidebar_Category_Stories`| Farmer Success Stories | কৃষক অভিজ্ঞতা ও সাফল্য |
| `Sidebar_Category_Machinery`| Machinery & Equipment | যন্ত্রপাতি ও আধুনিক চাষাবাদ |
| `Sidebar_Category_Storage`| Godown & Storage | গুদাম সংরক্ষণ ও ব্যবস্থাপনা |
| `Sidebar_Category_Market` | Local Market Rates | স্থানীয় হাট ও বাজার দর |
| `Sidebar_Category_Fertilizer`| Fertilizer & Pest Advice | সার, বীজ ও বালাইনাশক তথ্য |
| `Sidebar_Geo_Title` | Regional Filter | এলাকা ফিল্টার |
| `Sidebar_Geo_All` | All Bangladesh | সমগ্র বাংলাদেশ |
| `Sidebar_Geo_MyDistrict` | My District | আমার জেলা |
| `Sidebar_MyActivity` | My Activity | আমার কার্যকলাপ |
| `Sidebar_MyPosts` | My Posts | আমার পোস্টসমূহ |
| `Sidebar_MyAnswers` | My Solutions | আমার দেওয়া সমাধান |
| `Sidebar_Saved` | Bookmarked Posts | সংরক্ষিত পোস্ট |

### 15.3 Post Composer Strings
| Key | English Value | Bengali Value (বাংলা) |
| :--- | :--- | :--- |
| `Composer_Placeholder` | Write your crop update, problem, or farming experience... | আপনার ফসলের অবস্থা, সমস্যা বা অভিজ্ঞতা বিস্তারিত লিখুন... |
| `Composer_Tab_Story` | General Experience | সাধারণ অভিজ্ঞতা ও গল্প |
| `Composer_Tab_Help` | Ask for Help (Problem) | সাহায্য প্রয়োজন (কৃষি সমস্যা) |
| `Composer_Tab_Tip` | Agritech Tip | আধুনিক টিপস ও পরামর্শ |
| `Composer_Crop_Label` | Crop Name | ফসলের নাম |
| `Composer_Crop_Select` | Select Crop | ফসল নির্বাচন করুন |
| `Composer_Issue_Label` | Problem Category | সমস্যার ধরন |
| `Composer_Issue_Select` | Select Issue Category | সমস্যার ধরন নির্বাচন করুন |
| `Composer_Age_Label` | Crop Age / Stage | ফসলের বয়স বা পর্যায় |
| `Composer_Area_Label` | Affected Area | আক্রান্ত জমির পরিমাণ |
| `Composer_Urgency_Label`| Urgency Level | জরুরি অবস্থা |
| `Composer_Urgency_Normal`| Normal Consultative | সাধারণ পরামর্শ |
| `Composer_Urgency_Moderate`| Moderate Urgency | মাঝারি জরুরি |
| `Composer_Urgency_High` | High Urgency (Crop Dying)| অত্যন্ত জরুরি (ফসল নষ্ট হচ্ছে) |
| `Composer_Attach_Photo` | Photo (Max 5) | ছবি যুক্ত করুন (সর্বোচ্চ ৫টি) |
| `Composer_Attach_Video` | Video (Max 60s) | ভিডিও (সর্বোচ্চ ৬০ সেকেন্ড) |
| `Composer_Attach_Voice` | Record Voice Note | ভয়েস রেকর্ড করুন |
| `Composer_Publish_Btn` | Publish Post | পোস্ট প্রকাশ করুন |
| `Composer_Uploading` | Uploading post... | পোস্ট আপলোড হচ্ছে... |

### 15.4 Post Card and Interaction Strings
| Key | English Value | Bengali Value (বাংলা) |
| :--- | :--- | :--- |
| `Post_Seeking_Solution` | Seeking Solution | সমাধান খুঁজছেন |
| `Post_Solved_Status` | Solved | সমাধান হয়েছে |
| `Post_Accepted_Header` | Accepted Verified Solution | গৃহীত সঠিক সমাধান |
| `Post_Approved_By_Author`| Approved by Post Author | লেখক কর্তৃক গৃহীত |
| `Post_Solver_Points` | Solver earned +50 Krishi Points | সমাধানদাতা +৫০ কৃষি পয়েন্ট অর্জন করেছেন |
| `Post_See_More` | See More... | আরও দেখুন... |
| `Post_See_Less` | See Less | কম দেখুন |
| `Post_Reaction_Helpful` | Helpful | সহায়ক |
| `Post_Reaction_Like` | Like | পছন্দ |
| `Post_Reaction_Love` | Love | সাধুবাদ |
| `Post_Comment_Btn` | Comment | মন্তব্য |
| `Post_WhatsApp_Share` | WhatsApp Share | হোয়াটসঅ্যাপে শেয়ার |
| `Post_Bookmark_Btn` | Bookmark | সংরক্ষণ |
| `Comment_Placeholder` | Write a helpful solution or comment... | একটি সাহায্যকারী সমাধান বা মন্তব্য লিখুন... |
| `Comment_Reply_Btn` | Reply | উত্তর দিন |
| `Comment_Accept_Solution`| Mark as Accepted Solution | এটি সঠিক সমাধান হিসেবে গ্রহণ করুন |
| `Comment_Official_Badge`| Official Extension Advisory | অফিসিয়াল পরামর্শ (DAE) |

### 15.5 Right Sidebar and Live Widget Strings
| Key | English Value | Bengali Value (বাংলা) |
| :--- | :--- | :--- |
| `Widget_Emergency_Title`| Urgent Needs in Your District | আপনার জেলার জরুরি সমস্যাসমূহ |
| `Widget_Emergency_CTA` | Provide Solution Fast | দ্রুত সমাধান দিন |
| `Widget_Weather_Title` | Local Weather & Spray Advisor | স্থানীয় আবহাওয়া ও স্প্রে পরামর্শ |
| `Widget_Leaderboard_Title`| Top Mentors of the Month | চলতি মাসের সেরা কৃষি বন্ধু |
| `Widget_Leaderboard_Pts` | Points | পয়েন্ট |
| `Widget_Leaderboard_Sols`| Solutions | সমাধান |
| `Widget_Helpline_Title` | Emergency Agricultural Helplines | সরকারি জরুরি কৃষি হেল্পলাইন |
| `Widget_Helpline_Krishi` | Krishi Call Center: 16123 (Toll Free) | কৃষি কল সেন্টার: ১৬১২৩ (টোল ফ্রি) |
| `Widget_Helpline_Disaster`| Disaster Warning: 1090 | দুর্যোগের আগাম বার্তা: ১০৯০ |
| `Widget_Helpline_National`| National Info Service: 333 | জাতীয় তথ্য ও সেবা: ৩৩৩ |

---

## 16. Detailed User Journeys and End-to-End Task Scenarios

The following realistic scenarios illustrate how the Community Hub functions across distinct user roles in rural Bangladesh:

### 16.1 Scenario A: Farmer Solaiman Reports Late Blight in Potato Crop (Help Needed)
1. Context: Solaiman is a smallholder farmer in Shibganj, Bogura, cultivating 2 bighas of Diamond variety potato. During morning inspection, he notices dark water-soaked lesions spreading rapidly across foliage.
2. Step-by-Step Action:
   - Solaiman opens KrishiLink on his smartphone and taps the "কমিউনিটি" (Community) button in the top navigation bar.
   - At the top of the central feed, he taps the composer card trigger. The expanded modal appears.
   - He selects the tab "সাহায্য প্রয়োজন" (Ask for Help).
   - Diagnostic Fields:
     - Crop: Selects "গোল আলু" (Potato).
     - Issue Category: Selects "রোগবালাই ও ছত্রাক" (Fungal / Disease).
     - Growth Stage: Selects "৫০ দিন" (50 Days / Tuber Bulking).
     - Affected Area: Enters "২ বিঘা" (2 Bighas).
     - Urgency: Taps "অত্যন্ত জরুরি - ফসল নষ্ট হচ্ছে" (High Urgency - Crop in Danger).
   - Photos: Taps "ছবি যুক্ত করুন" (Attach Photo), opens smartphone camera, and takes two close-up photos of the leaf lesions and one wide shot of the field.
   - Voice Note: Unsure how to spell "Mancozeb" in Bengali, he taps "ভয়েস রেকর্ড" (Record Voice) and speaks in his local dialect: "ভাই আমার আলুর গাছে পাতায় তামাটে দাগ হইয়া পাতা ঝইলা যাইতাছে। কুয়াশার পরে এই অবস্থা। কার্বেনডাজিম স্প্রে কইরা কোনো কাম হয় নাই। জরুরি সমাধান দেন।" (Brothers, my potato leaves are turning brown and scorching after the heavy fog. Carbendazim didn't work. Please give urgent advice.)
   - He taps "পোস্ট প্রকাশ করুন" (Publish Post).
3. System Response:
   - Post is published with a crimson High Urgency border.
   - Broadcast alert is sent to registered farmers and agronomists in Shibganj and Bogura Sadar.
   - Post appears at the top of the Right Sidebar Emergency Dispatch Board.

### 16.2 Scenario B: DAE Officer Monjurul Delivers the Verified Accepted Solution
1. Context: Dr. Monjurul Islam is a DAE Agricultural Extension Officer based in Bogura.
2. Step-by-Step Action:
   - Monjurul receives an in-app notification: "জরুরি কৃষি সাহায্য: শিবগঞ্জ উপজেলায় আলু ফসলে জরুরি সমস্যা।" (Urgent Farm Help: Potato crisis in Shibganj).
   - He opens the post on his desktop workstation. He views Solaiman's high-resolution leaf photos and listens to the audio description.
   - Diagnosing the condition conclusively as Potato Late Blight (Phytophthora infestans), Monjurul clicks "মন্তব্য করুন" (Comment).
   - He writes an authoritative prescription:
     "সোলেমান ভাই, এটি আলুর মারাত্মক নাবী ধসা (Late Blight) রোগ। কুয়াশাচ্ছন্ন আবহাওয়ায় এ রোগ দ্রুত ছড়ায়। কার্বেনডাজিমে এ রোগ দমন হবে না। অবিলম্বে নিম্নলিখিত ব্যবস্থা নিন:
     ১. জমিতে সেচ দেওয়া সাময়িকভাবে বন্ধ রাখুন।
     ২. আজ বিকেলেই সাইমোক্সানিল + ম্যানকোজেব (যেমন কার্জেট বা সিকিউর) প্রতি লিটার পানিতে ২ গ্রাম হারে মিশিয়ে পুরো গাছে ভালোভাবে স্প্রে করুন।
     ৩. আক্রমণ বেশি হলে ৩ দিন পর ডাইমেথোমর্ফ (যেমন এক্রোবেট) স্প্রে করুন।"
   - He attaches an official DAE reference image showing proper nozzle pressure and safety gloves.
   - He submits the comment.
3. Acceptance Ceremony:
   - Solaiman receives an instant alert on his phone: "কৃষি কর্মকর্তা ড. মনজুরুল ইসলাম আপনার পোস্টে সমাধান দিয়েছেন।" (Agronomist Monjurul posted a solution).
   - Solaiman reads the advice, purchases the recommended formulation at his local union bazar, and applies it that afternoon.
   - Within 48 hours, the blight lesions dry up and healthy new shoots emerge.
   - Solaiman returns to his post, navigates to Monjurul's comment, and taps "এটি সঠিক সমাধান হিসেবে গ্রহণ করুন" (Accept this as Correct Solution).
   - Monjurul's comment is pinned at the top of the post in the Emerald Accepted Solution box.
   - Monjurul is credited with +50 Krishi Karma Points, advancing his standing on the Monthly Leaderboard.

### 16.3 Scenario C: Machinery Owner Rafiq Shares Combine Harvester Demonstration
1. Context: Rafiq owns a Kubota combine harvester in Dinajpur and wants to demonstrate operational efficiency to local farmers preparing for Aman harvest.
2. Step-by-Step Action:
   - Rafiq opens Community Hub and selects tab "অভিজ্ঞতা ও গল্প" (General Experience).
   - He writes a post: "দিনাজপুর সদরে আজ ব্রি ধান-৪৯ কর্তন সম্পন্ন করলাম। মাত্র ৪৫ মিনিটে এক বিঘা জমির ধান কাটা, মাড়াই ও বস্তাবন্দী করা হয়েছে। সনাতন পদ্ধতির চেয়ে খরচ ৬০% কম।" (Completed cutting BRRI Dhan-49 in Dinajpur Sadar today. One bigha harvested, threshed, and bagged in just 45 minutes. Costs 60% less than manual harvesting.)
   - He attaches a 45-second short video showing the harvester clean-cutting lodging paddy and two photos of grain sacks.
   - Tags: #কম্বাইন_হারভেস্টার #ধান_কাটাই #দিনাজপুর.
   - Submits post.
3. Outcome: Local farmers in Dinajpur leave 14 comments inquiring about Rafiq's hourly rental rates, and 3 farmers initiate direct machinery booking requests through Rafiq's linked KrishiLink equipment listing.

---

> End of Specification Document.  
> Prepared for the KrishiLink Core Engineering and UX Architecture Team.  
> Standard: Enterprise Agritech Specification (Clean Layout, Zero Emojis). 
