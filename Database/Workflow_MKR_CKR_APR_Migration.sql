-- =================================================================================
-- SQL Server Migration Script: MKR-CKR-APR Campaign Workflow & Audit History
-- Database: NotificationDB
-- Tables:   dbo.tbl_MessageCampaign, dbo.tbl_CampaignAuditHistory
-- Project:  SankalpAPI / Sankalp Setu
-- Date:     2026-10-22
-- =================================================================================

-- 1. Create Campaign Audit History Table
IF OBJECT_ID(N'dbo.tbl_CampaignAuditHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tbl_CampaignAuditHistory
    (
        AuditId          BIGINT IDENTITY(1,1) NOT NULL,
        CampaignId       BIGINT               NOT NULL,
        Action           NVARCHAR(50)         NOT NULL,
        FromStatus       NVARCHAR(50)         NULL,
        ToStatus         NVARCHAR(50)         NOT NULL,
        ActionBy         NVARCHAR(150)        NOT NULL,
        ActionRole       NVARCHAR(50)         NULL,
        ActionDate       DATETIME             NOT NULL CONSTRAINT DF_tbl_CampaignAuditHistory_ActionDate DEFAULT (GETUTCDATE()),
        Remarks          NVARCHAR(1000)       NULL,
        ActionIP         NVARCHAR(50)         NULL,

        CONSTRAINT PK_tbl_CampaignAuditHistory PRIMARY KEY CLUSTERED (AuditId ASC)
    );

    CREATE NONCLUSTERED INDEX IX_tbl_CampaignAuditHistory_CampaignId
        ON dbo.tbl_CampaignAuditHistory (CampaignId ASC, ActionDate DESC);

    PRINT 'SUCCESS: Table [dbo.tbl_CampaignAuditHistory] created successfully.';
END
ELSE
BEGIN
    PRINT 'INFO: Table [dbo.tbl_CampaignAuditHistory] already exists.';
END
GO

-- 2. Add Workflow Columns to dbo.tbl_MessageCampaign if missing
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'RejectionReason')
BEGIN
    ALTER TABLE dbo.tbl_MessageCampaign ADD RejectionReason NVARCHAR(1000) NULL;
    PRINT 'SUCCESS: Column [RejectionReason] added to dbo.tbl_MessageCampaign.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'VerifiedRemarks')
BEGIN
    ALTER TABLE dbo.tbl_MessageCampaign ADD VerifiedRemarks NVARCHAR(1000) NULL;
    PRINT 'SUCCESS: Column [VerifiedRemarks] added to dbo.tbl_MessageCampaign.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'ApprovedRemarks')
BEGIN
    ALTER TABLE dbo.tbl_MessageCampaign ADD ApprovedRemarks NVARCHAR(1000) NULL;
    PRINT 'SUCCESS: Column [ApprovedRemarks] added to dbo.tbl_MessageCampaign.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.tbl_MessageCampaign', N'U') AND name = N'LastActionRole')
BEGIN
    ALTER TABLE dbo.tbl_MessageCampaign ADD LastActionRole NVARCHAR(50) NULL;
    PRINT 'SUCCESS: Column [LastActionRole] added to dbo.tbl_MessageCampaign.';
END
GO

-- 3. Normalize Legacy Statuses in dbo.tbl_MessageCampaign
UPDATE dbo.tbl_MessageCampaign
SET CampaignStatus = CASE 
    WHEN CampaignStatus = 'Draft' THEN 'DRAFT'
    WHEN CampaignStatus = 'Pending Verification' THEN 'SUBMITTED_FOR_VERIFICATION'
    WHEN CampaignStatus = 'Pending Approval' THEN 'PENDING_APPROVAL'
    WHEN CampaignStatus = 'Approved' THEN 'APPROVED'
    WHEN CampaignStatus = 'Scheduled' THEN 'APPROVED'
    WHEN CampaignStatus = 'Sending' THEN 'SENDING'
    WHEN CampaignStatus = 'Completed' THEN 'SENT'
    WHEN CampaignStatus = 'Partially Failed' THEN 'SEND_FAILED'
    WHEN CampaignStatus = 'Failed' THEN 'SEND_FAILED'
    ELSE CampaignStatus
END
WHERE CampaignStatus IN (
    'Draft', 'Pending Verification', 'Pending Approval', 
    'Approved', 'Scheduled', 'Sending', 'Completed', 
    'Partially Failed', 'Failed'
);
PRINT 'SUCCESS: Status values normalized to standard uppercase workflow statuses.';
GO
