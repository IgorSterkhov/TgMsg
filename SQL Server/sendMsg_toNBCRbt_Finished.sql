USE [Adm]
GO

/****** Object:  StoredProcedure [bot].[sendMsg_toNBCRbt_Finished]    Script Date: 15.03.2026 22:49:27 ******/
DROP PROCEDURE IF EXISTS [bot].[sendMsg_toNBCRbt_Finished]
GO

/****** Object:  StoredProcedure [bot].[sendMsg_toNBCRbt_Finished]    Script Date: 15.03.2026 22:49:27 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE     PROCEDURE [bot].[sendMsg_toNBCRbt_Finished]
	@execid bigint
	, @package nvarchar(128)
AS
/*DECLARE
	@execid bigint = 71937
	, @package nvarchar(128) = '0_Main'
	*/

DECLARE @crlf nvarchar(128) = NCHAR(10) + NCHAR(13)
DECLARE @sPackage nvarchar(128) = REPLACE(@package, '.dtsx', '')
DECLARE @msg nvarchar(MAX) = N'���������� �������� : �������� ������ � ���������� ����' + @crlf + N'����������: ' + @sPackage

DECLARE @eid bigint = @execid --894931
DECLARE @dStart datetime, @dEnd datetime
DECLARE @iDur int, @iRes int, @sErrMsg nvarchar(MAX)
DECLARE @i int = 24

WHILE @i > 0
BEGIN 

SELECT
	   @dStart= [start_time]
      ,@dEnd = [end_time]
      ,@iDur = [execution_duration] / 1000 / 60
      ,@iRes = [execution_result]
  FROM [SSISDB].[internal].[executable_statistics] s
  JOIN [SSISDB].[internal].executables e on e.executable_id = s.executable_id
  JOIN [SSISDB].[internal].[executions] ex on ex.execution_id = s.execution_id
  where 
  1=1 
    --CAST([end_time] as date) = '20201023'
	--and ex.package_name = @sPackage + '.dtsx'
	and e.executable_name = @sPackage
	and ex.execution_id = @eid
  ORDER BY [start_time] desc

--��������� ���� �������� ���� ������ �� ��������, ���� 2 ���
IF @iRes is null WAITFOR DELAY '00:00:05'
IF @iRes is null SET @i += -1
IF @iRes is not null SET @i = 0
END

  SET @msg = @msg + @crlf + N'����� ������ : ' + ISNULL(FORMAT(@dStart, 'dd.MM.yyyy HH:mm:ss'), N'�� ����������')
  SET @msg = @msg + @crlf + N'����� ���������� : ' + ISNULL(FORMAT(@dEnd, 'dd.MM.yyyy HH:mm:ss'), N'�� ����������')
  SET @msg = @msg + @crlf + N'������ ���������� : ' + ISNULL(iif(@iRes = 0, N'�������', N'������'), N'�� ����������')

  IF @dStart is null SET @msg = @msg + @crlf + N'SSIS ExecutionID:' + CAST(@eid as nvarchar(32))
  print @msg 

INSERT INTO bot.ArchiveMsg_bots
([bot], [chat], [execid], [package], [start], [end], [dur], [res], msg, moddate)
SELECT 'my_bot', 'my_chat', @execid, @package, @dStart, @dEnd, @iDur, @iRes, @msg, GETDATE()

EXEC [bot].[sendMsg_toNBCRbt] @msg, @iRes

IF @iRes != 0
BEGIN
	SELECT 
		@sErrMsg = string_agg(message, ', ')
	FROM SSISDB.internal.operation_messages
	where operation_id = @eid
	and message_type in (/*100, 110, */120)
	and message_source_type = 40

	SET @msg = N'����� ������ : ' + @crlf + @sErrMsg

	INSERT INTO bot.ArchiveMsg_bots
	([bot], [chat], [execid], [package], [start], [end], [dur], [res], msg, moddate)
	SELECT 'my_bot', 'my_chat', @execid, @package, @dStart, @dEnd, @iDur, @iRes, @msg, GETDATE()

	EXEC [bot].[sendMsg_toNBCRbt] @msg, 0
END
GO
