USE [BD_Tracks];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------


-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[Tracks]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Tracks];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'Tracks'
CREATE TABLE [dbo].[Tracks] (
    [ID_Track] bigint IDENTITY(1,1) NOT NULL,
    [Track] varchar(260)  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [ID_Track] in table 'Tracks'
ALTER TABLE [dbo].[Tracks]
ADD CONSTRAINT [PK_Tracks]
    PRIMARY KEY CLUSTERED ([ID_Track] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------