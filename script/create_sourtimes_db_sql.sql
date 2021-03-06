USE [master]
GO
/****** Object:  Database [Sourtimes]    Script Date: 2/6/2019 3:04:01 PM ******/
CREATE DATABASE [Sourtimes]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'Sourtimes', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL14.MSSQLSERVER\MSSQL\DATA\Sourtimes.mdf' , SIZE = 20480KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )
 LOG ON 
( NAME = N'Sourtimes_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL14.MSSQLSERVER\MSSQL\DATA\Sourtimes_log.ldf' , SIZE = 15360KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )
GO
ALTER DATABASE [Sourtimes] SET COMPATIBILITY_LEVEL = 100
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [Sourtimes].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [Sourtimes] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [Sourtimes] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [Sourtimes] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [Sourtimes] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [Sourtimes] SET ARITHABORT OFF 
GO
ALTER DATABASE [Sourtimes] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [Sourtimes] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [Sourtimes] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [Sourtimes] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [Sourtimes] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [Sourtimes] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [Sourtimes] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [Sourtimes] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [Sourtimes] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [Sourtimes] SET  DISABLE_BROKER 
GO
ALTER DATABASE [Sourtimes] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [Sourtimes] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [Sourtimes] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [Sourtimes] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [Sourtimes] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [Sourtimes] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [Sourtimes] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [Sourtimes] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [Sourtimes] SET  MULTI_USER 
GO
ALTER DATABASE [Sourtimes] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [Sourtimes] SET DB_CHAINING OFF 
GO
ALTER DATABASE [Sourtimes] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [Sourtimes] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [Sourtimes] SET DELAYED_DURABILITY = DISABLED 
GO
USE [Sourtimes]
GO

IF NOT EXISTS ( SELECT name FROM master.sys.server_principals WHERE name = 'dba_io_user')
BEGIN
	CREATE LOGIN [dba_io_user] WITH PASSWORD = 'USERPASSWORDHERE';
END
GO

CREATE USER [dba_io_user] FOR LOGIN [dba_io_user] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_ddladmin] ADD MEMBER [dba_io_user]
GO
ALTER ROLE [db_datareader] ADD MEMBER [dba_io_user]
GO
ALTER ROLE [db_datawriter] ADD MEMBER [dba_io_user]
GO

GRANT CREATE TYPE TO [dba_io_user]
GO
GRANT DELETE TO [dba_io_user]
GO
GRANT EXECUTE TO [dba_io_user]
GO
GRANT INSERT TO [dba_io_user]
GO
GRANT SELECT TO [dba_io_user]
GO
GRANT SHOWPLAN TO [dba_io_user]
GO
GRANT UPDATE TO [dba_io_user]
GO


/****** Object:  Table [dbo].[Basliks]    Script Date: 2/6/2019 3:04:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Basliks](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Baslik] [nvarchar](50) NULL,
 CONSTRAINT [PK_Baslik] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ClientState]    Script Date: 2/6/2019 3:04:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ClientState](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IpAddress] [varchar](16) NULL,
	[Flag] [int] NOT NULL,
	[RemainRegCount] [int] NOT NULL,
 CONSTRAINT [PK_ClientState] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Entries]    Script Date: 2/6/2019 3:04:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Entries](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[BaslikId] [int] NULL,
	[SuserId] [int] NULL,
	[Date] [datetime] NULL,
	[Descr] [nvarchar](max) NOT NULL,
	[Active] [bit] NOT NULL,
 CONSTRAINT [PK_Entries] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Susers]    Script Date: 2/6/2019 3:04:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Susers](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Suser] [nvarchar](40) NULL,
	[Password] [char](128) NULL,
	[DummyMail] [char](32) NULL,
	[IsActive] [bit] NULL,
 CONSTRAINT [PK_Susers] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [NCI_Baslik]    Script Date: 2/6/2019 3:04:01 PM ******/
CREATE NONCLUSTERED INDEX [NCI_Baslik] ON [dbo].[Basliks]
(
	[Baslik] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [NCI_Entries_BaslikId_SuserId]    Script Date: 2/6/2019 3:04:01 PM ******/
CREATE NONCLUSTERED INDEX [NCI_Entries_BaslikId_SuserId] ON [dbo].[Entries]
(
	[BaslikId] ASC
)
INCLUDE ( 	[SuserId]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ClientState] ADD  CONSTRAINT [DF_ClientState_Flag]  DEFAULT ((0)) FOR [Flag]
GO
ALTER TABLE [dbo].[ClientState] ADD  CONSTRAINT [DF_ClientState_RemainRegCount]  DEFAULT ((3)) FOR [RemainRegCount]
GO
ALTER TABLE [dbo].[Entries] ADD  CONSTRAINT [DF_Entries_Active]  DEFAULT ((1)) FOR [Active]
GO
ALTER TABLE [dbo].[Susers] ADD  CONSTRAINT [DF_Susers_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Entries]  WITH NOCHECK ADD  CONSTRAINT [FK_Entries_Baslik] FOREIGN KEY([BaslikId])
REFERENCES [dbo].[Basliks] ([Id])
GO
ALTER TABLE [dbo].[Entries] CHECK CONSTRAINT [FK_Entries_Baslik]
GO
ALTER TABLE [dbo].[Entries]  WITH NOCHECK ADD  CONSTRAINT [FK_Entries_Susers] FOREIGN KEY([SuserId])
REFERENCES [dbo].[Susers] ([Id])
GO
ALTER TABLE [dbo].[Entries] CHECK CONSTRAINT [FK_Entries_Susers]
GO
ALTER TABLE [dbo].[Susers]  WITH NOCHECK ADD  CONSTRAINT [FK_Susers_Susers] FOREIGN KEY([Id])
REFERENCES [dbo].[Susers] ([Id])
GO
ALTER TABLE [dbo].[Susers] CHECK CONSTRAINT [FK_Susers_Susers]
GO
/****** Object:  StoredProcedure [dbo].[IsRegistrationAllowed]    Script Date: 2/6/2019 3:04:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[IsRegistrationAllowed]
	@Modify BIT,
	@IpAddr VARCHAR(16),
	@IsAllowed BIT OUTPUT
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @RemCount INT

	SET @IsAllowed = 0
	
	IF NOT EXISTS (SELECT Flag, RemainRegCount FROM ClientState WHERE IpAddress = @IpAddr) 
	BEGIN
		IF @Modify = 1 INSERT INTO ClientState(IpAddress) VALUES(@IpAddr)

		SET @IsAllowed = 1
	END
	ELSE 
	BEGIN
		SELECT @RemCount = RemainRegCount FROM ClientState WHERE IpAddress = @IpAddr;

		IF @RemCount > 0
		BEGIN
			IF @Modify = 1 UPDATE ClientState SET RemainRegCount=@RemCount-1 WHERE IpAddress=@IpAddr
			SET @IsAllowed = 1;
		END
	END
END
GO
USE [master]
GO
ALTER DATABASE [Sourtimes] SET  READ_WRITE 
GO
