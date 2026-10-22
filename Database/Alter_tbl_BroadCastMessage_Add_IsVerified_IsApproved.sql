-- =================================================================================
-- SQL Server Migration Script: Add IsVerified and IsApproved to tbl_BroadCastMessage
-- Database: NotificationDB
-- Table:    dbo.tbl_BroadCastMessage
-- Project:  SankalpAPI
-- Date:     2026-10-21
-- =================================================================================

-- Ensure we are using the correct database (change if different in your environment)
-- USE [NotificationDB];
-- GO

-- =================================================================================
-- 1. ADD COLUMNS ONLY IF THEY DO NOT ALREADY EXIST
-- =================================================================================

-- 1.1 Add IsVerified Column (BIT, NOT NULL, DEFAULT 0)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') 
      AND name = N'IsVerified'
)
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage
    ADD IsVerified BIT NOT NULL CONSTRAINT DF_tbl_BroadCastMessage_IsVerified DEFAULT (0);
    PRINT 'SUCCESS: Column [IsVerified] successfully added to dbo.tbl_BroadCastMessage.';
END
ELSE
BEGIN
    PRINT 'INFO: Column [IsVerified] already exists in dbo.tbl_BroadCastMessage.';
END
GO

-- 1.2 Add IsApproved Column (BIT, NOT NULL, DEFAULT 0)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') 
      AND name = N'IsApproved'
)
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage
    ADD IsApproved BIT NOT NULL CONSTRAINT DF_tbl_BroadCastMessage_IsApproved DEFAULT (0);
    PRINT 'SUCCESS: Column [IsApproved] successfully added to dbo.tbl_BroadCastMessage.';
END
ELSE
BEGIN
    PRINT 'INFO: Column [IsApproved] already exists in dbo.tbl_BroadCastMessage.';
END
GO

-- =================================================================================
-- 2. (OPTIONAL) AUDIT COLUMNS (VerifiedBy, VerifiedIP, ApprovedBy, ApprovedIP, etc.)
-- =================================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') AND name = N'VerifiedBy')
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage ADD VerifiedBy NVARCHAR(255) NULL;
    PRINT 'SUCCESS: Column [VerifiedBy] added to dbo.tbl_BroadCastMessage.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') AND name = N'VerifiedDate')
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage ADD VerifiedDate DATETIME NULL;
    PRINT 'SUCCESS: Column [VerifiedDate] added to dbo.tbl_BroadCastMessage.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') AND name = N'VerifiedIP')
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage ADD VerifiedIP NVARCHAR(50) NULL;
    PRINT 'SUCCESS: Column [VerifiedIP] added to dbo.tbl_BroadCastMessage.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') AND name = N'ApprovedBy')
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage ADD ApprovedBy NVARCHAR(255) NULL;
    PRINT 'SUCCESS: Column [ApprovedBy] added to dbo.tbl_BroadCastMessage.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') AND name = N'ApprovedDate')
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage ADD ApprovedDate DATETIME NULL;
    PRINT 'SUCCESS: Column [ApprovedDate] added to dbo.tbl_BroadCastMessage.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_BroadCastMessage', N'U') AND name = N'ApprovedIP')
BEGIN
    ALTER TABLE dbo.tbl_BroadCastMessage ADD ApprovedIP NVARCHAR(50) NULL;
    PRINT 'SUCCESS: Column [ApprovedIP] added to dbo.tbl_BroadCastMessage.';
END
GO

-- =================================================================================
-- 3. STORED PROCEDURES (Alternative to Parameterized SQL Queries)
-- =================================================================================

-- 3.1 Stored Procedure: sp_update_broadcast_message_verified
CREATE OR ALTER PROCEDURE dbo.sp_update_broadcast_message_verified
    @p_bmsg_id     BIGINT,
    @p_is_verified BIT
AS
BEGIN
    SET NOCOUNT ON;

    -- Check if record exists
    IF NOT EXISTS (SELECT 1 FROM dbo.tbl_BroadCastMessage WHERE Bmsg_id = @p_bmsg_id)
    BEGIN
        SELECT 0 AS RowsAffected, 0 AS RecordExists;
        RETURN;
    END

    -- Update only the verification status
    UPDATE dbo.tbl_BroadCastMessage
    SET IsVerified = @p_is_verified
    WHERE Bmsg_id = @p_bmsg_id;

    SELECT @@ROWCOUNT AS RowsAffected, 1 AS RecordExists;
END;
GO

-- 3.2 Stored Procedure: sp_update_broadcast_message_approved
CREATE OR ALTER PROCEDURE dbo.sp_update_broadcast_message_approved
    @p_bmsg_id     BIGINT,
    @p_is_approved BIT
AS
BEGIN
    SET NOCOUNT ON;

    -- Check if record exists
    IF NOT EXISTS (SELECT 1 FROM dbo.tbl_BroadCastMessage WHERE Bmsg_id = @p_bmsg_id)
    BEGIN
        SELECT 0 AS RowsAffected, 0 AS RecordExists;
        RETURN;
    END

    -- Update only the approval status
    UPDATE dbo.tbl_BroadCastMessage
    SET IsApproved = @p_is_approved
    WHERE Bmsg_id = @p_bmsg_id;

    SELECT @@ROWCOUNT AS RowsAffected, 1 AS RecordExists;
END;
GO
