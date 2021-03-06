[CmdletBinding()] 
param( 
[Parameter(Position=0, Mandatory=$true)] [string]$filepath
) 

ADD-PSSNAPIN SqlServerProviderSnapin100 
ADD-PSSNAPIN SqlServerCmdletSnapin100

$DATA = Import-Csv $filepath
$SQLINSTANCE = "localhost\play"

$count = 0;
FOREACH ($LINE in $DATA)

{
    if($LINE."Delegate User" -eq "")
    {
        $delusr = "";
    }
    else
    {
        $delusr = $LINE."Delegate User";
        $deluser = $delusr.replace("'","''");
    }
    
    $usr = $LINE.User.replace("'","''");
    $Action = $LINE.Action.replace("'","''");
    $Section = $LINE.Section.replace("'","''");
    
    $SQLQUERY=”INSERT INTO [SalesforceAudit].[dbo].[SalesforceAuditTemp] ([Date],[UserName],[Action],[Section],[DelegatedUser]) VALUES ('“;
    $SQLQUERY+= $LINE.Date.Substring(0,$LINE.Date.length-3).trim()+"','"+$usr+"'"+",'"+$Action+"','"+$Section+"','"+$deluser+"')";
    
    Invoke-Sqlcmd –Query $SQLQuery -ServerInstance $SQLINSTANCE
    
}

Invoke-Sqlcmd –Query "EXEC ProcessNewAuditImport" -ServerInstance $SQLINSTANCE -Database "SalesforceAudit"