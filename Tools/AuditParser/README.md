AuditParser
==========
Script written in Powershell.

It will parse an Audit Log File downloaded from your Salesforce Org and Dump it into a SQL DB.

There are 2 versions, one for Production and one for Staging environments.


Steps for use:
==========

1. Run the SQL code on the target DB.
2. Update line 10 on both PS1 files to point to the correct SQL Server instance
3. Run the Powershell scripts with one parameter: the path to the audit csv file
