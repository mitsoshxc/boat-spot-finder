-- ============================================================================
-- Smoke-test seed: two Marinas + two PlaceOwner invitations
-- ============================================================================
-- Purpose:
--   Enables TESTING.md §6-§10 (PlaceOwner flows + ownership enforcement)
--   before AdminController exists (Phase 6). Idempotent — safe to re-run.
--
-- Raw invite tokens (NOT in DB — register URLs use these):
--   Marina A: smoke-marinaA-2026-05-20
--   Marina B: smoke-marinaB-2026-05-20
--
-- Register URLs (paste into browser after `dotnet run`):
--   https://localhost:7214/account/invite-register?token=smoke-marinaA-2026-05-20
--   https://localhost:7214/account/invite-register?token=smoke-marinaB-2026-05-20
--
-- Hashes below match TokenHasher.Hash (SHA-256 of UTF-8 bytes, uppercase hex).
-- ============================================================================

USE BoatSpotFinder;
GO

DECLARE @AdminId    NVARCHAR(450) = '30000000-0000-0000-0000-000000000001';
DECLARE @MarinaAId  UNIQUEIDENTIFIER = 'A0000000-0000-0000-0000-00000000000A';
DECLARE @MarinaBId  UNIQUEIDENTIFIER = 'B0000000-0000-0000-0000-00000000000B';
DECLARE @InviteAId  UNIQUEIDENTIFIER = 'A1000000-0000-0000-0000-000000000001';
DECLARE @InviteBId  UNIQUEIDENTIFIER = 'B1000000-0000-0000-0000-000000000001';
DECLARE @ExpiresAt  DATETIMEOFFSET = DATEADD(HOUR, 48, SYSUTCDATETIME());

-- Marina A
IF NOT EXISTS (SELECT 1 FROM Marinas WHERE Id = @MarinaAId)
BEGIN
    INSERT INTO Marinas (Id, Name, Description, Address, Region, Phone, Latitude, Longitude, DefaultPricePerDay)
    VALUES (
        @MarinaAId,
        'Marina A (smoke-test)',
        'Seeded for §6-§10 smoke tests. PlaceOwner A administers this marina.',
        '1 Smoke Quay',
        'Attica',
        '+30 210 0000001',
        37.9420, 23.6470,
        50.00
    );
END

-- Marina B (needed for §10 ownership enforcement)
IF NOT EXISTS (SELECT 1 FROM Marinas WHERE Id = @MarinaBId)
BEGIN
    INSERT INTO Marinas (Id, Name, Description, Address, Region, Phone, Latitude, Longitude, DefaultPricePerDay)
    VALUES (
        @MarinaBId,
        'Marina B (smoke-test)',
        'Second marina so PlaceOwner B can cross-attempt MarinaA endpoints and get 403.',
        '2 Smoke Quay',
        'Attica',
        '+30 210 0000002',
        37.9430, 23.6480,
        60.00
    );
END

-- Invitation A → placeownerA@smoke.test for Marina A
IF NOT EXISTS (SELECT 1 FROM Invitations WHERE Id = @InviteAId)
BEGIN
    INSERT INTO Invitations (Id, Email, Token, MarinaId, ExpiresAt, InvitedById)
    VALUES (
        @InviteAId,
        'placeownera@smoke.test',
        'AA47776F9BCA891987E8A1A24D744BC8044D5485EABD44690EC08435728DAD44',
        @MarinaAId,
        @ExpiresAt,
        @AdminId
    );
END
ELSE
BEGIN
    -- refresh expiry on re-run so token is always valid for 48h from now
    UPDATE Invitations SET ExpiresAt = @ExpiresAt, IsUsed = 0 WHERE Id = @InviteAId;
END

-- Invitation B → placeownerb@smoke.test for Marina B
IF NOT EXISTS (SELECT 1 FROM Invitations WHERE Id = @InviteBId)
BEGIN
    INSERT INTO Invitations (Id, Email, Token, MarinaId, ExpiresAt, InvitedById)
    VALUES (
        @InviteBId,
        'placeownerb@smoke.test',
        '4CED163B7364B44747D94935FB2854B04D806595B3CB088284E5393ECCE81A3C',
        @MarinaBId,
        @ExpiresAt,
        @AdminId
    );
END
ELSE
BEGIN
    UPDATE Invitations SET ExpiresAt = @ExpiresAt, IsUsed = 0 WHERE Id = @InviteBId;
END

PRINT '--- Seeded ---';
PRINT 'Marina A:    A0000000-0000-0000-0000-00000000000A';
PRINT 'Marina B:    B0000000-0000-0000-0000-00000000000B';
PRINT 'Invite A URL: /account/invite-register?token=smoke-marinaA-2026-05-20';
PRINT 'Invite B URL: /account/invite-register?token=smoke-marinaB-2026-05-20';

-- ============================================================================
-- Cleanup (uncomment to undo)
-- ============================================================================
-- DELETE FROM Invitations WHERE Id IN (@InviteAId, @InviteBId);
-- DELETE FROM MarinaAdmins WHERE MarinaId IN (@MarinaAId, @MarinaBId);
-- DELETE FROM Spots WHERE MarinaId IN (@MarinaAId, @MarinaBId);
-- DELETE FROM Marinas WHERE Id IN (@MarinaAId, @MarinaBId);
-- DELETE FROM AspNetUsers WHERE Email IN ('placeownera@smoke.test', 'placeownerb@smoke.test');
