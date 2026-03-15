/*USE master;
GO

DROP DATABASE IF EXISTS DeployDB; --промежуточна бд где включен параметр TRUSTWORTHY ON;
DROP DATABASE IF EXISTS TrustedAsmDB; --БД куда создадим конечную CLR
GO

CREATE DATABASE DeployDB;
GO

ALTER DATABASE DeployDB
SET TRUSTWORTHY ON;
GO

CREATE DATABASE TrustedAsmDB;
GO*/


USE Adm
DROP FUNCTION IF EXISTS [bot].[sp_sendMsg]
GO

USE DeployDB;
GO

DECLARE @AssemblyName nvarchar(128) = N'TgMsg'
DECLARE @AssemblyFile nvarchar(4000) = N'D:\Dev\CLR\TgMsg.dll'
DECLARE @fnName nvarchar(32)= N'SendMsg'
		
DECLARE @SQLfnName nvarchar(32)= N'sp_sendMsg'
		,@SQLfnSchema nvarchar(32)= N'adm'

DECLARE @AssemblyBin varbinary(max) 
DECLARE @hash varbinary(64);

DECLARE @sqlDropAssembly nvarchar(128) = N'DROP ASSEMBLY IF EXISTS ' + @AssemblyName
DECLARE @sqlCreateAssembly nvarchar(128) = N'CREATE ASSEMBLY ' + @AssemblyName + ' FROM @AssemblyFile WITH PERMISSION_SET = EXTERNAL_ACCESS   ' 

DECLARE @sqlDropFunction nvarchar(128) = N'DROP FUNCTION IF EXISTS ' + @SQLfnSchema + '.' + @SQLfnName
		--,@sqlCreateFunction nvarchar(128) = N'CREATE ASSEMBLY ' + @AssemblyName + ' FROM @AssemblyFile'

--SELECT @sqlCreateAssembly
EXEC sp_executesql @sqlDropAssembly
EXEC sp_executesql @sqlCreateAssembly, N'@AssemblyFile nvarchar(4000)', @AssemblyFile = @AssemblyFile

SELECT @AssemblyBin = content FROM sys.assembly_files
WHERE name = @AssemblyFile

SELECT @hash = HASHBYTES('SHA2_512', @AssemblyBin);

---USE TrustedAsmDB
USE Adm

EXEC sp_executesql @sqlDropFunction
EXEC sp_executesql @sqlDropAssembly

EXEC sys.sp_drop_trusted_assembly @hash = @hash
EXEC sys.sp_add_trusted_assembly @hash = @hash, @description = @AssemblyName;

EXEC sp_executesql @sqlDropAssembly
EXEC sp_executesql @sqlCreateAssembly, N'@AssemblyFile nvarchar(4000)', @AssemblyFile = @AssemblyFile

GO 

CREATE OR ALTER FUNCTION [bot].[sp_sendMsg](@botID [nvarchar](128), @chatID [nvarchar](128), @msg [nvarchar](4000), @proxyUrl [nvarchar](4000))
RETURNS [nvarchar](4000) WITH EXECUTE AS CALLER
AS 
EXTERNAL NAME [TgMsg].[TgMsg.TgMsg].[SendMsgProxy]
GO

 -- SELECT bot.sp_sendMsg('**botID here**', '**Chat ID here **', 'Test')