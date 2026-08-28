

<!-- Start of picture text -->
ye<br>(&<br><!-- End of picture text -->





<!-- Start of picture text -->
ee<br><!-- End of picture text -->

_KrishiLink — Project Proposal Report_ 

# **<u>Table of Contents</u>** 

|**1. Introduction**...................................................................................................................................................**3**|
|---|
|**2. Problem Statement**........................................................................................................................................**3**|
|**3. SDG Alignment**..............................................................................................................................................**4**|
|**4. Project Description and Solution Approach**...............................................................................................**4**|
|**5. Key Functionalities and Features**................................................................................................................**5**|
|**6. System Architecture**......................................................................................................................................**6**|
|**7. Database Design (Entity Relationship Diagram)**........................................................................................**8**|
|**8. Background Study and Gap Analysis**........................................................................................................**10**|
|**9. Scope of the First Version (MVP)**..............................................................................................................**11**|
|**10. Conclusion**..................................................................................................................................................**12**|



Page 2 of 12 

_KrishiLink — Project Proposal Report_ 

# **<u>1. Introduction</u>** 

Small and medium-scale farmers across Bangladesh routinely face three interconnected challenges: they cannot afford to individually own expensive farm machinery, they lack safe and affordable short-term storage for harvested produce, and they have limited access to reliable, localized crop and cultivation guidance. KrishiLink is a web-based platform designed to address all three challenges through a single, integrated system. 

KrishiLink connects three groups of stakeholders — farmers, agricultural equipment owners, and godown (storage) owners and adds a rule-based crop and weather advisory service on top, so that a farmer can plan a crop, rent the machinery needed to cultivate it, and secure storage for the harvest, all from one platform. 

## **1.1 Project Title** 

KrishiLink - Farmer Support and Agricultural Resource-Sharing Platform. 

## **1.2 Core Idea** 

The guiding principle behind KrishiLink is simple: farmers should be able to use agricultural resources when they need them, without bearing the full cost of ownership. Equipment and godown owners who are not using their assets at a given time can list them on KrishiLink; farmers who need those assets only temporarily can discover, book, and pay for them online, while a rule-based advisory engine helps them decide what to grow and how to care for it. 

# **<u>2. Problem Statement</u>** 

Agricultural machinery such as tractors, power tillers, harvesters, seeders, and sprayers are expensive relative to the income of an average small or medium-scale farmer in Bangladesh. At the same time, national statistics show that the average operated farm area per household has been shrinking — from about 1.47 acres in 2009 to about 1.29 acres in 2019 which makes individual ownership of costly machinery even less economical for most farming households. 

The problem is compounded by several related gaps in the current system: 

- Equipment owners who possess machinery often do not use it continuously throughout the year, leaving expensive assets idle for long periods while nearby farmers cannot access them. 

- Farmers frequently need equipment for only a few hours or days at a specific point in the cultivation cycle (e.g., land preparation or harvesting), which does not justify outright purchase. 

- Harvested crops often need short-term or seasonal storage before farmers can sell at a fair price, but formal, bookable storage (godown) space is difficult to locate and reserve. 

- Farmers frequently lack clear, localized information on which crop suits their land, soil, and the current season, and cultivation guidance is scattered across informal sources. 

- There is no single, trusted digital channel in the local context that brings machinery rental, storage booking, and crop advisory together  farmers must rely on separate, often informal and unreliable, arrangements for each need. 

Page 3 of 12 

_KrishiLink — Project Proposal Report_ 

KrishiLink is proposed as a single online platform where farmers can rent equipment, rent storage space, and receive crop recommendations and cultivation guidance, reducing the cost and uncertainty of independent, informal arrangements. 

# **<u>3. SDG Alignment</u>** 

KrishiLink is aligned with several of the United Nations Sustainable Development Goals (SDGs). Its primary alignment is with SDG 9, and it also directly supports SDG 2 and SDG 8. 

## **3.1 SDG 9 — Industry, Innovation and Infrastructure (Primary)** 

KrishiLink builds digital infrastructure for agricultural resource-sharing. By digitizing the discovery, booking, and payment process for machinery and storage, it introduces a modern, technology-driven layer of infrastructure into a sector that has traditionally relied on informal, word-of-mouth arrangements, encouraging innovation and mechanization in agriculture. 

## **3.2 SDG 2 — Zero Hunger (Supporting)** 

By improving farmers' access to machinery, storage, and crop guidance, KrishiLink supports more efficient cultivation and post-harvest handling, which contributes to food security and reduces post-harvest loss caused by inadequate storage. 

## **3.3 SDG 8 — Decent Work and Economic Growth (Supporting)** 

Equipment owners and godown owners can generate additional income by renting out otherwise idle resources, creating new small-business opportunities and improving the economic return on existing agricultural assets. 

# **<u>4. Project Description and Solution Approach</u>** 

KrishiLink is architected as a role-based web application in which farmers, equipment owners, godown owners, and an administrator each interact with the system through a dedicated portal. The platform is organized around three core workflows, described below. 

## **4.1 Equipment Rental Workflow** 

Equipment Owner registers and lists equipment → Farmer searches and filters available equipment → Farmer sends a rental request for specific dates → Owner accepts or rejects the request → Equipment is used by the farmer → Rental is marked complete and payment is recorded. 

## **4.2 Godown / Storage Rental Workflow** 

Godown Owner registers and lists available storage space → Farmer searches nearby godowns and checks capacity/price → Farmer books the required storage for a duration → Farmer stores produce → Booking is marked complete. 

## **4.3 Crop Advisory Workflow** 

Page 4 of 12 

_KrishiLink — Project Proposal Report_ 

Farmer submits farming details (location, season, soil information, land size, irrigation availability →  System applies rule-based logic against the crop database and current weather data  →  System returns recommended crops with growing requirements and cultivation guidance. 

## **4.4 Illustrative User Journey** 

Rahim is a farmer who wants to prepare his land for the upcoming season. He creates an account on KrishiLink and enters his location and basic soil information. The system shows him a list of suitable crops with growing requirements. He selects a recommendation and reviews the cultivation guide. To prepare the land, he searches for a nearby tractor, checks its availability, and requests it for two days; the equipment owner accepts the request through their portal. After harvesting, Rahim searches for a nearby godown, checks the available capacity and price, and books storage for his produce. At every stage, Rahim can track the status of his requests and bookings, and view his full transaction history, from a single dashboard. 

# **<u>5. Key Functionalities and Features</u>** 

## **5.1 Module 1 — Agricultural Equipment Rental** 

## **_Equipment Owner can:_** 

- Register and log in to a dedicated owner portal 

- Add equipment with name, type, description and images 

- Set hourly or daily rental price and location 

- Set and update availability status 

- Accept or reject incoming rental requests 

- View complete rental history 

## **_Farmer can:_** 

- Browse and search equipment by type and location 

- Check rental price and current availability 

- Request equipment for specific hours or days 

- Track booking status and view rental history 

## **5.2 Module 2 — Godown / Storage Rental** 

## **_Godown Owner can:_** 

- Register/log in and list a godown with location and capacity 

- Set daily or monthly rental price and available capacity 

- Accept or reject booking requests and manage current bookings 

## **_Farmer can:_** 

- Search nearby godowns and compare capacity and price 

- Select a required storage duration and book space 

- Track booking status and manage active/past storage bookings 

Page 5 of 12 

_KrishiLink — Project Proposal Report_ 

## **5.3 Module 3 — Crop Recommendation and Farming Advisory** 

Farmers provide location, season, soil information (including soil pH where available), land size, previous crop, and irrigation availability. In return, the system provides a rule-based recommendation covering: suitable crops, growing season, soil and water requirements, approximate growing duration, fertilizer needs, a basic cultivation guide, common pests/diseases, and precautions. 

## **5.4 Weather-Based Suggestions** 

KrishiLink integrates a weather API (e.g., OpenWeather) with a predefined rule set  for example, warning about sensitive crops before heavy rain, or suggesting irrigation adjustments during high temperatures implemented at a manageable rule-based level rather than as a full machine-learning forecasting model, which keeps the feature realistic for an academic-scale first version. 

## **5.5 User Roles** 

|**Role**|**Key Capabilities**|
|---|---|
|Farmer|Search/rent equipment, book godowns, receive crop recommendations, view<br>guidance, manage bookings and transactions|
|Equipment Owner|List equipment, manage availability and pricing, approve/reject requests, view<br>rental history|
|Godown Owner|List storage facilities, manage capacity and pricing, approve/reject bookings|



# **<u>6. System Architecture</u>** 

KrishiLink follows a layered, three-tier architecture that keeps presentation, business logic, and data access clearly separated. This separation makes the codebase easier to test, maintain, and extend, and lets each layer evolve independently as the project grows beyond its first version. 

Page 6 of 12 

### KrishiLink — 3-Tier System Architecture (MVP) 



<!-- Start of picture text -->
Web Browser (Desktop & Mobile-Responsive,<br>Bootstrap)<br>Presentation Layer — KrishiLink.WebUI (ASP.NET Core MVC, Razor Views)<br>DTOs / Vaid igaGg“777-72oo coon coco crcccc cece cece ecec cece ce eeeeeceeeeeeeee cece ftir integration 2+ seeeseseseseeseeeeeeeseees<br>Business Logic Layer — KrishiLink.BLL<br>c >)<br>EquipmentRentalServicecale, availabilty check) (rate | GodownBookingService(capacity validation) | CropAdvisoryServicebased recommendation)(rule- Ve “otiSSISary WeatherAdvisoryService(weather-based rules) lq.) a imi<br>X / \<br>Repository Calls<br>| Future / Post-MVP (Optional&<br>Data Access Layer — KrishiLink.DAL (Entity Framework Core) ;|Advanced — not in current scope) j<br>j '' AVML Crop Recommendation & ' 11<br>Deaoeey EquipmentRepository & GodownRepository & CropRepository & WeatherDataRepository & | | Pest Detection ' 1 :<br>BookingRepository BookingRepository RecommendationRepository TransactionRepository jee! Hl<br>! ' ' !<br>ApplicationDbContext (EF Core) F 3| ae !ho}<br>1 Oe 1<br>1 1<br>EF Core LING / Parameterized Queries<br>KrishiLinkDB — MsSQL 8.0<br><!-- End of picture text -->

_KrishiLink — Project Proposal Report_ 

## **6.3 Data Access Layer — KrishiLink.DAL** 

The data access layer uses Entity Framework Core with the repository pattern (UserRepository, EquipmentRepository, GodownRepository, CropRepository, WeatherDataRepositoryand their related booking repositories) built on a shared ApplicationDbContext. All data access to the underlying MsSQL database is performed through parameterised EF Core LINQ queries, which keeps data access consistent and reduces the risk of injection vulnerabilities. 

## **6.4 Technology Stack** 

|**Layer**|**Technology**|
|---|---|
|Frontend / Presentation|ASP.NET Core MVC, Razor Views (C#), Bootstrap (CSS styling only)|
|Backend / Business Logic|C#, ASP.NET Core MVC|
|Data Access|Entity Framework Core|
|Database|MsSQL 8.0(MsSQL Workbench for management)|
|External Service|Weather API (e.g., OpenWeather)|
|IDE / Tooling|Visual Studio 2023|



The diagram also marks two items AI/ML-based crop recommendation and pest detection, and GPS/locationbased search as future, post-MVP scope. These are intentionally kept out of the first version so the group can deliver a stable, fully-functional rule-based system within the course timeline, with a clear integration path shown in the architecture for later enhancement. 

# **—** **<u>7. Database Design Entity Relationship Diagram</u>** 

The KrishiLink database is centered on the Users entity, which plays every role in the system (farmer, equipment owner, godown owner) through its Role attribute, avoiding duplicate identity tables for what is fundamentally the same account model. Bookings and transactions are modelled as separate entities from the underlying resources so that historical records remain intact even if equipment or godown listings are later changed. 

Page 8 of 12 



<!-- Start of picture text -->
= &<br>UNI 1<br>\<br>.<br>g<br>g<br>| § | \4/ a<br>g<br>? | 0<br>1 f4 e {<br>a<br>—=<br>1<br>1<br>3 2 - :<br>g im<br>lan io : A \<br><!-- End of picture text -->

_KrishiLink — Project Proposal Report_ 

## **7.1 Core Entities and Relationships** 

|**Entity**|**Purpose**|**Key Relationship**|
|---|---|---|
|Users|Stores account details and role (Farmer / Equipment<br>Owner / Godown Owner / Admin)|One user owns many Equipment /<br>Godown<br>listings;<br>makes<br>many<br>Bookings and Transactions|
|Equipment|Machinery listed for rent (name, rate, availability)|Owned by one User (1:N); booked<br>through EquipmentBookings(1:N)|
|EquipmentBookings|A farmer's rental request for a piece of equipment|Belongs to one Equipment and one<br>User (farmer) (N:1 each)|
|Godowns|Storage facilities listed for rent (capacity, rate)|Owned by one User (1:N); booked<br>through GodownBookings(1:N)|
|GodownBookings|A farmer's storage reservation|Belongs to one Godown and one User<br>(farmer) (N:1 each)|
|Crops|Reference data on crop growing requirements|Linked<br>to<br>many<br>CropRecommendations(1:N)|
|CropRecommendations|A recommendation generated for a specific farmer|Belongs to one User and one Crop (N:1<br>each)|
|WeatherData|Logged weather readings by location, used by the<br>advisoryrules|Standalone reference data consumed<br>bythe Business Logic Layer|
|Transactions|Payment records tied to a booking|Belongs to one User (N:1); references<br>the related booking|



# **<u>8. Background Study and Gap Analysis</u>** 

Several digital platforms already attempt to solve pieces of the problem KrishiLink addresses, mostly in the equipment-rental space, and mostly outside Bangladesh. Reviewing them clarifies where KrishiLink differs and why an integrated, locally-focused platform is still needed. 

## **8.1 Existing Systems Reviewed** 

|**System**|**Focus Area**|**Notable Characteristics**|
|---|---|---|
|Trringo (Mahindra, India)|Farm equipment rental|Large-scale, on-demand tractor and machinery<br>rental; integrated with government subsidy transfer<br>for Custom Hiring Centres|
|EM3 Agri Services (India)|Pay-per-use farm services|Farmers book specific tasks (e.g., plowing) and pay<br>per use rather than rentingthe machine itself|
|JFarm Services / KisanRaja /<br>Sonalika app (India)|Equipment<br>rental<br>marketplace|Connect tractor/implement owners directly with<br>nearby farmers for negotiated rentals|
|AgriShare|Peer-to-peer<br>equipment<br>rental|Farmer-to-farmer listing and booking of machinery<br>with mobilepayment|
|Gold Farm (various regions)|Tractor and solar pump<br>rental|Mobile-app based rental connecting farmers with<br>machinery/pump owners|



Page 10 of 12 

_KrishiLink — Project Proposal Report_ 

|**System**|**Focus Area**|**Notable Characteristics**|
|---|---|---|
|Bangladesh context (per ACI PLC<br>industry review)|Proposed / early-stage|Bangladesh and Nepal are noted as candidates for<br>pilot rollouts of similar rental platforms; no<br>dominant, integrated localplatformyet exists|



## **8.2 Gap Analysis** 

Comparing these systems against the KrishiLink proposal highlights four consistent gaps: 

- Single-purpose scope: almost every existing platform reviewed focuses only on equipment rental (or, in EM3's case, per-task service booking). None of them combine machinery rental with storage/godown booking and crop advisory in one account and one dashboard, which is central to KrishiLink's design. 

- No integrated crop or weather advisory: the reviewed platforms help a farmer find machinery but do not help them decide what to grow or how weather conditions should influence that decision. KrishiLink's rule-based CropAdvisoryService and WeatherAdvisoryService close this gap. 

- Limited presence in Bangladesh: the reviewed platforms are concentrated in India and other markets; industry analysis explicitly lists Bangladesh only as a market for future pilot rollouts, meaning small and medium Bangladeshi farmers currently have no equivalent, locally-oriented digital option. 

- No unified storage booking: post-harvest storage is handled informally or not at all by the reviewed equipment-rental apps, despite storage access being one of the reasons farmers sell produce quickly at lower prices. KrishiLink's Godown module directly targets this gap. 

KrishiLink is therefore positioned not as a copy of an existing rental app, but as a locally-focused, integrated alternative that unifies three needs mechanization, storage, and crop guidance that are currently served, if at all, by separate and mostly informal channels in the Bangladeshi context. 

# **<u>9. Scope of the First Version (MVP)</u>** 

To keep the project realistic for a university group working within a single semester, the first version is scoped to a Minimum Viable Product (MVP), with clearly identified advanced features left for later phases. 

## **9.1 Must-Have (MVP)** 

- User registration/login with role-based access 

- Equipment listing, search, and rental request flow 

- Godown listing, search, and booking flow 

- Rule-based crop recommendation and cultivation guide 

- Weather information and basic weather-based crop suggestions 

- Booking history 

## **9.2 Future / Advanced (Post-MVP)** 

- Online payment gateway integration 

- SMS notifications 

- GPS/location-based search 

- Reviews and ratings 

Page 11 of 12 

_KrishiLink — Project Proposal Report_ 

- AI/ML-based crop recommendation and pest/disease image detection 

- Demand-based rental price prediction and a native mobile application 

# **<u>10. Conclusion</u>** 

KrishiLink brings together agricultural equipment rental, storage rental, and rule-based crop advisory into a single, role-based web platform built on C#, ASP.NET Core MVC, Entity Framework Core, and MsSQL. By focusing the first version on a realistic, well-scoped MVP and clearly separating presentation, business logic, and data access layers, the project is designed to be both academically demonstrable within the course timeline and practically extendable toward the more advanced features identified for future phases. In doing so, KrishiLink directly targets a gap that current equipment-rental platforms most of which are single-purpose and largely absent from the Bangladeshi market  do not address. 

Page 12 of 12 

