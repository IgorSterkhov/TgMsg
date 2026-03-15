USE [Adm]
GO

/****** Object:  UserDefinedFunction [bot].[sp_sendMsg]    Script Date: 15.03.2026 22:51:46 ******/
DROP FUNCTION [bot].[sp_sendMsg]
GO

/****** Object:  UserDefinedFunction [bot].[sp_sendMsg]    Script Date: 15.03.2026 22:51:46 ******/
SET ANSI_NULLS OFF
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE FUNCTION [bot].[sp_sendMsg](@botID [nvarchar](128), @chatID [nvarchar](128), @msg [nvarchar](max))
RETURNS [nvarchar](4000) WITH EXECUTE AS CALLER
AS 
EXTERNAL NAME [TgMsg].[TgMsg.TgMsg].[SendMsg]
GO


