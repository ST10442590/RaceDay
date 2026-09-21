# RaceDay – API Endpoint Plan

**Base URL:** `/api`  **Format:** JSON  **Auth:** JWT bearer token returned by `/api/auth/login`, sent as `Authorization: Bearer <token>`.

**Roles:** *None* = public, *Any* = any logged-in user, *Organiser*, *Participant*.
"Owner" means the Organiser who created the event.

**Common failure codes:** `400 Bad Request` (validation failed), `401 Unauthorized` (no/invalid token), `403 Forbidden` (wrong role or not the owner), `404 Not Found`.

---

## 1. Authentication

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates a new Participant account so a runner, walker or cyclist can enter events. | None | `{ firstName, lastName, email, password, phoneNumber, dateOfBirth, gender, city }` | 201 Created – user profile (no password). 400 – invalid fields / weak password. 409 Conflict – email already registered. |
| POST | /api/auth/login | Checks email and password and returns a JWT containing the user's id and role. | None | `{ email, password }` | 200 OK – `{ token, expiresAt, userId, role, firstName }`. 401 – wrong email or password. 403 – account deactivated. |

> Organiser accounts are seeded by the system administrator, so public registration always creates a Participant.

## 2. User Profile

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| GET | /api/users/me | Returns the logged-in user's profile for the profile page. | Any | None | 200 OK – profile. 401. |
| PUT | /api/users/me | Updates the logged-in user's own personal details. | Any | `{ firstName, lastName, phoneNumber, dateOfBirth, gender, city }` | 200 OK – updated profile. 400 – validation. 401. |
| PUT | /api/users/me/password | Changes the logged-in user's password after confirming the current one. | Any | `{ currentPassword, newPassword }` | 204 No Content. 400 – current password wrong / new password weak. 401. |

## 3. Event Types (lookup)

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| GET | /api/event-types | Lists event types (Running, Walking, Cycling) for filters and the create-event form. | None | None | 200 OK – list of `{ eventTypeId, typeName }`. |

## 4. Events

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| GET | /api/events | Lists events for browsing; optional query filters `?type=&city=&province=&from=&to=&status=`. | None | None | 200 OK – list of event summaries (may be empty). 400 – invalid filter value. |
| GET | /api/events/{id} | Returns one event with its categories for the event detail page. | None | None | 200 OK – event with categories. 404 – event not found. |
| GET | /api/events/mine | Lists the events created by the logged-in Organiser for their dashboard. | Organiser | None | 200 OK – list of events. 401. 403. |
| POST | /api/events | Creates a new event owned by the logged-in Organiser. | Organiser | `{ eventTypeId, title, description, eventDate, startTime, venueName, city, province, latitude, longitude, routeUrl, registrationDeadline }` | 201 Created – new event with `Location` header. 400 – validation (e.g. deadline after event date). 401. 403. |
| PUT | /api/events/{id} | Updates an event's details or status (e.g. Open, Closed, Completed, Cancelled). | Organiser (owner) | `{ eventTypeId, title, description, eventDate, startTime, venueName, city, province, latitude, longitude, routeUrl, registrationDeadline, status }` | 200 OK – updated event. 400. 403 – not the owner. 404. |
| DELETE | /api/events/{id} | Deletes an event and its categories. | Organiser (owner) | None | 204 No Content. 403 – not the owner. 404. 409 Conflict – event has confirmed enrolments (cancel it instead). |
| POST | /api/events/{id}/image | Uploads an event banner image to Azure Blob Storage and saves the returned URL on the event (Part 3). | Organiser (owner) | multipart/form-data: `file` (jpg/png, max 5 MB) | 200 OK – `{ imageUrl }`. 400 – wrong file type/size. 403. 404. |
| GET | /api/events/{id}/weather | Returns the live weather forecast for the event's location and date using its coordinates. | None | None | 200 OK – `{ temperature, conditions, windSpeed, rainChance }`. 404 – event not found. 503 – weather service unavailable. |

## 5. Categories

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | Lists the categories of an event with places remaining. | None | None | 200 OK – list of categories. 404 – event not found. |
| GET | /api/categories/{id} | Returns one category's details. | None | None | 200 OK – category. 404. |
| POST | /api/events/{eventId}/categories | Adds a category (e.g. 21.1km Half Marathon) to an event. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants, startTime, minAge }` | 201 Created – new category. 400. 403. 404 – event not found. 409 – category name already exists for this event. |
| PUT | /api/categories/{id} | Updates a category's details. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants, startTime, minAge }` | 200 OK – updated category. 400 – e.g. max below current enrolments. 403. 404. |
| DELETE | /api/categories/{id} | Removes a category from an event. | Organiser (owner) | None | 204 No Content. 403. 404. 409 – category has enrolments. |

## 6. Event Enrolments

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | /api/enrolments | Enters the logged-in Participant into an event by choosing a category. | Participant | `{ categoryId }` | 201 Created – enrolment with race number. 400 – below minimum age. 404 – category not found. 409 – already entered, category full, or registration closed. |
| GET | /api/enrolments/me | Lists the logged-in Participant's enrolments for the "My Events" page. | Participant | None | 200 OK – list with event, category and status. 401. 403. |
| DELETE | /api/enrolments/{id} | Cancels the Participant's own enrolment before the registration deadline (sets status to Cancelled). | Participant (own) | None | 204 No Content. 403 – not their enrolment. 404. 409 – deadline has passed. |
| GET | /api/events/{eventId}/enrolments | Lists everyone entered in an event, grouped by category, for the Organiser. | Organiser (owner) | None | 200 OK – list of enrolments with participant details. 403. 404. |

## 7. Results

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | /api/results | Captures a participant's result for a confirmed enrolment. | Organiser (owner) | `{ enrolmentId, finishTime, resultStatus }` (`finishTime` "hh:mm:ss", required only when status is Finished) | 201 Created – result. 400 – invalid time/status combo. 403. 404 – enrolment not found. 409 – result already captured. |
| PUT | /api/results/{id} | Corrects a previously captured result. | Organiser (owner) | `{ finishTime, resultStatus }` | 200 OK – updated result. 400. 403. 404. |
| DELETE | /api/results/{id} | Removes a result captured in error. | Organiser (owner) | None | 204 No Content. 403. 404. |
| GET | /api/events/{eventId}/results | Returns the results board for an event with positions per category. | None | None | 200 OK – results ordered by category and position. 404 – event not found. |
| GET | /api/results/me | Returns the logged-in Participant's personal history of finish times and positions. | Participant | None | 200 OK – list of results. 401. 403. |

---

## Design notes

- **Ownership checks:** Organiser endpoints check the event's `OrganiserId` matches the token's user id, so one organiser cannot edit another's events (403).
- **Positions are calculated, not stored:** `vw_EventResults` ranks finishers per category using `RANK()`, so correcting a time automatically updates every position.
- **One entry per category:** enforced by the `UQ_Enrolments_Participant_Category` constraint and returned as 409.
- **Soft cancel:** cancelling an enrolment sets `Status = 'Cancelled'` instead of deleting, so the organiser keeps a record.
