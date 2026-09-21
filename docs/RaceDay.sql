/* =====================================================================
   RaceDay - Event Management System
   PROG6212 POE Part 1 - Database Schema and Seed Data
   Target: Microsoft SQL Server (run in SSMS)
   Safe to re-run: drops and recreates RaceDayDB.
   ===================================================================== */

USE master;
GO

IF DB_ID(N'RaceDayDB') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDB;
END
GO

CREATE DATABASE RaceDayDB;
GO

USE RaceDayDB;
GO

/* ---------------------------------------------------------------------
   1. Roles (lookup: Organiser, Participant)
   --------------------------------------------------------------------- */
CREATE TABLE Roles (
    RoleId      INT IDENTITY(1,1) NOT NULL,
    RoleName    NVARCHAR(30)      NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);
GO

/* ---------------------------------------------------------------------
   2. Users (each user has exactly one role)
   --------------------------------------------------------------------- */
CREATE TABLE Users (
    UserId        INT IDENTITY(1,1) NOT NULL,
    RoleId        INT               NOT NULL,
    FirstName     NVARCHAR(50)      NOT NULL,
    LastName      NVARCHAR(50)      NOT NULL,
    Email         NVARCHAR(256)     NOT NULL,
    PasswordHash  NVARCHAR(256)     NOT NULL,
    PhoneNumber   NVARCHAR(20)      NULL,
    DateOfBirth   DATE              NULL,
    Gender        NVARCHAR(10)      NULL,
    City          NVARCHAR(60)      NULL,
    IsActive      BIT               NOT NULL CONSTRAINT DF_Users_IsActive  DEFAULT (1),
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles (RoleId),
    CONSTRAINT CK_Users_Gender CHECK (Gender IS NULL OR Gender IN (N'Male', N'Female', N'Other'))
);
GO

/* ---------------------------------------------------------------------
   3. EventTypes (lookup: Run, Walk, Cycle)
   --------------------------------------------------------------------- */
CREATE TABLE EventTypes (
    EventTypeId  INT IDENTITY(1,1) NOT NULL,
    TypeName     NVARCHAR(30)      NOT NULL,
    CONSTRAINT PK_EventTypes PRIMARY KEY (EventTypeId),
    CONSTRAINT UQ_EventTypes_TypeName UNIQUE (TypeName)
);
GO

/* ---------------------------------------------------------------------
   4. Events (created and owned by an Organiser)
   Captures name, description, date, location, distance and event type.
   Latitude/Longitude feed the live weather lookup; RouteUrl the route map;
   ImageUrl will hold the Azure Blob Storage URL in Part 3.
   --------------------------------------------------------------------- */
CREATE TABLE Events (
    EventId               INT IDENTITY(1,1) NOT NULL,
    OrganiserId           INT               NOT NULL,
    EventTypeId           INT               NOT NULL,
    Name                  NVARCHAR(120)     NOT NULL,
    Description           NVARCHAR(2000)    NULL,
    EventDate             DATE              NOT NULL,
    StartTime             TIME(0)           NOT NULL,
    DistanceKm            DECIMAL(6,2)      NOT NULL,
    VenueName             NVARCHAR(120)     NOT NULL,
    City                  NVARCHAR(60)      NOT NULL,
    Province              NVARCHAR(40)      NOT NULL,
    Latitude              DECIMAL(9,6)      NULL,
    Longitude             DECIMAL(9,6)      NULL,
    RouteUrl              NVARCHAR(500)     NULL,
    ImageUrl              NVARCHAR(500)     NULL,
    RegistrationDeadline  DATE              NOT NULL,
    Status                NVARCHAR(20)      NOT NULL CONSTRAINT DF_Events_Status    DEFAULT (N'Upcoming'),
    CreatedAt             DATETIME2(0)      NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt             DATETIME2(0)      NULL,
    CONSTRAINT PK_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Users      FOREIGN KEY (OrganiserId) REFERENCES Users (UserId),
    CONSTRAINT FK_Events_EventTypes FOREIGN KEY (EventTypeId) REFERENCES EventTypes (EventTypeId),
    CONSTRAINT CK_Events_Status   CHECK (Status IN (N'Upcoming', N'Open', N'Closed', N'Completed', N'Cancelled')),
    CONSTRAINT CK_Events_Deadline CHECK (RegistrationDeadline <= EventDate),
    CONSTRAINT CK_Events_Distance CHECK (DistanceKm > 0)
);
GO

/* ---------------------------------------------------------------------
   5. EventCategories (age or distance categories per event,
      e.g. Under 20, Senior, 10km, 21km)
   Deleting an event removes its categories.
   --------------------------------------------------------------------- */
CREATE TABLE EventCategories (
    CategoryId       INT IDENTITY(1,1) NOT NULL,
    EventId          INT               NOT NULL,
    CategoryName     NVARCHAR(80)      NOT NULL,
    CategoryType     NVARCHAR(10)      NOT NULL,
    DistanceKm       DECIMAL(6,2)      NULL,
    MinAge           INT               NOT NULL CONSTRAINT DF_EventCategories_MinAge   DEFAULT (0),
    MaxAge           INT               NULL,
    EntryFee         DECIMAL(8,2)      NOT NULL CONSTRAINT DF_EventCategories_EntryFee DEFAULT (0),
    MaxParticipants  INT               NOT NULL,
    StartTime        TIME(0)           NULL,
    CONSTRAINT PK_EventCategories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_EventCategories_Events FOREIGN KEY (EventId) REFERENCES Events (EventId) ON DELETE CASCADE,
    CONSTRAINT UQ_EventCategories_Event_Name     UNIQUE (EventId, CategoryName),
    CONSTRAINT UQ_EventCategories_Category_Event UNIQUE (CategoryId, EventId),
    CONSTRAINT CK_EventCategories_Type CHECK (CategoryType IN (N'Age', N'Distance')),
    CONSTRAINT CK_EventCategories_Distance CHECK (
        (CategoryType = N'Distance' AND DistanceKm IS NOT NULL AND DistanceKm > 0) OR
        (CategoryType = N'Age'      AND DistanceKm IS NULL)),
    CONSTRAINT CK_EventCategories_Age CHECK (MinAge >= 0 AND (MaxAge IS NULL OR MaxAge >= MinAge)),
    CONSTRAINT CK_EventCategories_Fee CHECK (EntryFee >= 0),
    CONSTRAINT CK_EventCategories_Max CHECK (MaxParticipants > 0)
);
GO

/* ---------------------------------------------------------------------
   6. Enrolments (records the link between Participant, Event and Category)
   Resolves the many-to-many between Participants and EventCategories.
   The composite foreign key guarantees the chosen category belongs to
   the chosen event. A participant can enter each event only once.
   --------------------------------------------------------------------- */
CREATE TABLE Enrolments (
    EnrolmentId    INT IDENTITY(1,1) NOT NULL,
    ParticipantId  INT               NOT NULL,
    EventId        INT               NOT NULL,
    CategoryId     INT               NOT NULL,
    RaceNumber     NVARCHAR(10)      NULL,
    Status         NVARCHAR(20)      NOT NULL CONSTRAINT DF_Enrolments_Status     DEFAULT (N'Confirmed'),
    EnrolledAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Enrolments_EnrolledAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Enrolments PRIMARY KEY (EnrolmentId),
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantId) REFERENCES Users (UserId),
    CONSTRAINT FK_Enrolments_EventCategories FOREIGN KEY (CategoryId, EventId)
        REFERENCES EventCategories (CategoryId, EventId) ON DELETE CASCADE,
    CONSTRAINT UQ_Enrolments_Participant_Event UNIQUE (ParticipantId, EventId),
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN (N'Pending', N'Confirmed', N'Cancelled'))
);
GO

/* ---------------------------------------------------------------------
   7. Results (one-to-zero-or-one with Enrolments)
   Organisers capture the finish time and finishing position.
   DNF / DNS / DQ results have no time or position.
   --------------------------------------------------------------------- */
CREATE TABLE Results (
    ResultId        INT IDENTITY(1,1) NOT NULL,
    EnrolmentId     INT               NOT NULL,
    FinishTime      TIME(0)           NULL,
    FinishPosition  INT               NULL,
    ResultStatus    NVARCHAR(10)      NOT NULL CONSTRAINT DF_Results_Status     DEFAULT (N'Finished'),
    RecordedById    INT               NOT NULL,
    RecordedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_Results_RecordedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Results PRIMARY KEY (ResultId),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentId),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments (EnrolmentId) ON DELETE CASCADE,
    CONSTRAINT FK_Results_Users FOREIGN KEY (RecordedById) REFERENCES Users (UserId),
    CONSTRAINT CK_Results_Status CHECK (ResultStatus IN (N'Finished', N'DNF', N'DNS', N'DQ')),
    CONSTRAINT CK_Results_TimeAndPosition CHECK (
        (ResultStatus = N'Finished' AND FinishTime IS NOT NULL AND FinishPosition IS NOT NULL AND FinishPosition > 0) OR
        (ResultStatus <> N'Finished' AND FinishTime IS NULL AND FinishPosition IS NULL))
);
GO

/* ---------------------------------------------------------------------
   Indexes on columns used for common lookups
   --------------------------------------------------------------------- */
CREATE INDEX IX_Events_OrganiserId      ON Events (OrganiserId);
CREATE INDEX IX_Events_EventDate        ON Events (EventDate);
CREATE INDEX IX_EventCategories_EventId ON EventCategories (EventId);
CREATE INDEX IX_Enrolments_EventId      ON Enrolments (EventId);
GO

/* ---------------------------------------------------------------------
   View: results board per event and category
   --------------------------------------------------------------------- */
CREATE VIEW vw_EventResults AS
SELECT
    e.EventId,
    e.Name AS EventName,
    c.CategoryId,
    c.CategoryName,
    u.UserId AS ParticipantId,
    u.FirstName + N' ' + u.LastName AS ParticipantName,
    en.RaceNumber,
    r.ResultId,
    r.FinishPosition,
    r.FinishTime,
    r.ResultStatus
FROM Results r
JOIN Enrolments      en ON en.EnrolmentId = r.EnrolmentId
JOIN EventCategories c  ON c.CategoryId   = en.CategoryId
JOIN Events          e  ON e.EventId      = en.EventId
JOIN Users           u  ON u.UserId       = en.ParticipantId;
GO

/* =====================================================================
   SEED DATA
   PasswordHash values are placeholders. Part 2 will replace them with
   real hashes so these accounts can log in.
   ===================================================================== */

INSERT INTO Roles (RoleName) VALUES (N'Organiser'), (N'Participant');

INSERT INTO EventTypes (TypeName) VALUES (N'Run'), (N'Walk'), (N'Cycle');

-- Organisers (RoleId 1) and Participants (RoleId 2)
INSERT INTO Users (RoleId, FirstName, LastName, Email, PasswordHash, PhoneNumber, DateOfBirth, Gender, City) VALUES
 (1, N'Lerato', N'Mokoena', N'lerato.mokoena@raceday.co.za', N'PLACEHOLDER_HASH', N'0821234567', '1985-03-14', N'Female', N'Centurion'),
 (1, N'Johan',  N'van Wyk', N'johan.vanwyk@raceday.co.za',   N'PLACEHOLDER_HASH', N'0837654321', '1979-11-02', N'Male',   N'Durban'),
 (2, N'Sipho',  N'Dlamini', N'sipho.dlamini@gmail.com',      N'PLACEHOLDER_HASH', N'0712345678', '1994-07-21', N'Male',   N'Soweto'),
 (2, N'Aisha',  N'Patel',   N'aisha.patel@outlook.com',      N'PLACEHOLDER_HASH', N'0798765432', '1998-01-09', N'Female', N'Pretoria'),
 (2, N'Thandi', N'Nkosi',   N'thandi.nkosi@gmail.com',       N'PLACEHOLDER_HASH', N'0765550123', '1990-09-30', N'Female', N'Pietermaritzburg');

-- Events: one completed (has results), two upcoming
INSERT INTO Events (OrganiserId, EventTypeId, Name, Description, EventDate, StartTime, DistanceKm,
                    VenueName, City, Province, Latitude, Longitude, RouteUrl, RegistrationDeadline, Status) VALUES
 (1, 1, N'Centurion Sunrise Road Race',
        N'Fast, flat road race around Centurion Lake with 10km and 21.1km options.',
        '2026-08-16', '06:00', 21.10, N'Centurion Lake Precinct', N'Centurion', N'Gauteng',
        -25.855200, 28.189800, N'https://www.strava.com/routes/placeholder-centurion', '2026-08-09', N'Completed'),
 (2, 3, N'Valley of a Thousand Hills Cycle Classic',
        N'Scenic 100km road cycling classic through the KZN hills, with age categories.',
        '2026-10-18', '06:30', 100.00, N'Hillcrest Park', N'Hillcrest', N'KwaZulu-Natal',
        -29.781900, 30.763800, N'https://www.strava.com/routes/placeholder-hillcrest', '2026-10-11', N'Open'),
 (1, 2, N'Soweto Heritage Charity Walk',
        N'Family-friendly walk past Vilakazi Street in support of local schools.',
        '2026-11-22', '07:00', 10.00, N'Orlando Stadium', N'Soweto', N'Gauteng',
        -26.234600, 27.906600, NULL, '2026-11-15', N'Upcoming');

-- Categories: distance categories (events 1 and 3) and age categories (event 2)
INSERT INTO EventCategories (EventId, CategoryName, CategoryType, DistanceKm, MinAge, MaxAge, EntryFee, MaxParticipants, StartTime) VALUES
 (1, N'10km Run',             N'Distance', 10.00, 12, NULL, 180.00, 500, '06:30'),
 (1, N'21.1km Half Marathon', N'Distance', 21.10, 16, NULL, 280.00, 300, '06:00'),
 (2, N'Under 20',             N'Age',       NULL, 14,   19, 350.00, 150, '06:30'),
 (2, N'Senior',               N'Age',       NULL, 20,   39, 550.00, 250, '06:30'),
 (2, N'Veteran 40+',          N'Age',       NULL, 40, NULL, 550.00, 150, '06:30'),
 (3, N'5km Family Walk',      N'Distance',  5.00,  0, NULL,  60.00, 800, '07:30'),
 (3, N'10km Walk',            N'Distance', 10.00, 12, NULL,  90.00, 400, '07:00');

-- Enrolments: Participant + Event + selected Category
INSERT INTO Enrolments (ParticipantId, EventId, CategoryId, RaceNumber, Status) VALUES
 (3, 1, 2, N'H101', N'Confirmed'),  -- Sipho:  Half Marathon   (completed event)
 (4, 1, 2, N'H102', N'Confirmed'),  -- Aisha:  Half Marathon   (completed event)
 (5, 1, 1, N'T201', N'Confirmed'),  -- Thandi: 10km Run        (completed event)
 (3, 2, 4, N'C301', N'Confirmed'),  -- Sipho:  Senior          (upcoming)
 (4, 2, 4, N'C302', N'Confirmed'),  -- Aisha:  Senior          (upcoming)
 (5, 3, 6, NULL,    N'Pending');    -- Thandi: 5km Family Walk (upcoming)

-- Results for the completed event, captured by its organiser (Lerato)
INSERT INTO Results (EnrolmentId, FinishTime, FinishPosition, ResultStatus, RecordedById) VALUES
 (1, '01:38:42', 1,    N'Finished', 1),
 (2, NULL,       NULL, N'DNF',      1),
 (3, '00:54:05', 1,    N'Finished', 1);
GO

/* Quick check */
SELECT 'Roles' AS TableName, COUNT(*) AS TotalRows FROM Roles
UNION ALL SELECT 'Users',           COUNT(*) FROM Users
UNION ALL SELECT 'EventTypes',      COUNT(*) FROM EventTypes
UNION ALL SELECT 'Events',          COUNT(*) FROM Events
UNION ALL SELECT 'EventCategories', COUNT(*) FROM EventCategories
UNION ALL SELECT 'Enrolments',      COUNT(*) FROM Enrolments
UNION ALL SELECT 'Results',         COUNT(*) FROM Results;

SELECT * FROM vw_EventResults ORDER BY EventId, CategoryId, FinishPosition;
GO