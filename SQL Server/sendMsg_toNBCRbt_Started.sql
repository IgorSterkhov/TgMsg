USE [Adm]
GO

/****** Object:  StoredProcedure [bot].[sendMsg_toNBCRbt_Started]    Script Date: 15.03.2026 22:50:04 ******/
DROP PROCEDURE IF EXISTS [bot].[sendMsg_toNBCRbt_Started]
GO

/****** Object:  StoredProcedure [bot].[sendMsg_toNBCRbt_Started]    Script Date: 15.03.2026 22:50:04 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE       PROCEDURE [bot].[sendMsg_toNBCRbt_Started]
	@execid bigint
AS
DECLARE @msg nvarchar(128) = N'Старт процесса: Загрузка данных и обновление куба'
DECLARE @crlf nvarchar(128) = NCHAR(10) + NCHAR(13)
DECLARE @eid bigint = @execid --894931

SET @msg = @msg + @crlf + N'ExecutionID:' + CAST(@eid as nvarchar(32))
EXEC [bot].[sendMsg_toNBCRbt] @msg, 0

GO


