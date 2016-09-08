
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 08/16/2016 14:49:39
-- Generated from EDMX file: C:\Users\Carrillo\Desktop\Proyecto-Sistemas-Operativos-master\Reproductor\Reproductor\ModelTracks.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
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