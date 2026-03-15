USE [Adm]
GO

/****** Object:  StoredProcedure [bot].[sendMsg_toNBCRbt]    Script Date: 15.03.2026 22:48:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


DROP PROCEDURE IF EXISTS [bot].[sendMsg_toNBCRbt]
GO 

CREATE OR ALTER       PROCEDURE [bot].[sendMsg_toNBCRbt](
	@msg nvarchar(MAX)
	,@isErr bit
)
AS
--EXEC [bot].[sendMsg_toNBCRbt] 'test test', 1

--���������
DECLARE @botName nvarchar(512) = N'my_bot'
DECLARE @chatName nvarchar(512) = 'my_chat'
--

EXEC bot.sendMsg @botName, @chatName, @msg, @isErr
GO


