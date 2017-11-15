<#
 .SYNOPSIS
    Calls DocumentDB REST APIs to create new collections

 .DESCRIPTION
    Calls DocumentDB REST APIs to create new collections

 .PARAMETER resourceGroupName
    The resource group where the DocumentDB will live

 .PARAMETER databaseAccountName
    The name of the database account in which the DocumentDB should be created
#>

param(
 [Parameter(Mandatory=$True)]
 [string]
 $resourceGroupName,

 [Parameter(Mandatory=$True)]
 [string]
 $databaseAccountName
)

# Inspired by code from https://russellyoung.net/2016/06/18/managing-documentdb-with-powershell/

Add-Type -AssemblyName System.Web

<#
.SYNOPSIS
    Get the current date in UTC as a formatted string
#>
function GetUtcDate() {
 	 $date = get-date
 	 $date = $date.ToUniversalTime();
 	 return $date.ToString("r", [System.Globalization.CultureInfo]::InvariantCulture)
}

<#
.SYNOPSIS
    Create a Base64 string that can be used as a header authorization key for a REST API call
#>
function CreateHeaderAuthKey([System.String]$Verb = '', [System.String]$ResourceId = '',
		[System.String]$ResourceType = '', [System.String]$Date = '', [System.String]$masterKey = '') {
	$keyBytes = [System.Convert]::FromBase64String($masterKey) 
	$text = @($Verb.ToLowerInvariant() + "`n" + $ResourceType.ToLowerInvariant() + "`n" + $ResourceId + "`n" + $Date.ToLowerInvariant() + "`n" + "" + "`n")
	$body =[Text.Encoding]::UTF8.GetBytes($text)
	$hmacsha = new-object -TypeName System.Security.Cryptography.HMACSHA256 -ArgumentList (,$keyBytes) 
	$hash = $hmacsha.ComputeHash($body)
	$signature = [System.Convert]::ToBase64String($hash)

	[System.Web.HttpUtility]::UrlEncode($('type=master&ver=1.0&sig=' + $signature))
 }

<#
.SYNOPSIS
    Build a hashtable with the headers for the REST API call
#>
function BuildHeaders([string]$action = "get", [string]$resType, [string]$resourceId, [string]$connectionKey) {
	$apiDate = GetUtcDate;
    $authz = CreateHeaderAuthKey -Verb $action -ResourceType $resType -ResourceId $resourceId -Date $apiDate -masterKey $connectionKey
    $headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
    $headers.Add("Authorization", $authz)
    $headers.Add("x-ms-version", '2017-02-22')
    $headers.Add("x-ms-date", $apiDate) 
    $headers
}


#******************************************************************************
# Script body
# Execution begins here
#******************************************************************************


# Get information about the database account
$databaseAccount = Get-AzureRmResource -ResourceType "Microsoft.DocumentDb/databaseAccounts" -ResourceGroupName $resourceGroupName -Name $databaseAccountName;
$databaseAccountKeys = Invoke-AzureRmResourceAction -Action "listKeys" -ResourceType "Microsoft.DocumentDb/databaseAccounts" -ResourceGroupName $resourceGroupName -Name $databaseAccountName -Force;

$baseUri = $databaseAccount.Properties.documentEndpoint;
$accountKey = $databaseAccountKeys.primaryMasterKey;


# Create a database
$uri = $baseUri + "dbs";
$dbName = "$resourceGroupName-db";
Write-Host "Creating DocumentDB database '$dbName'";
$headers = BuildHeaders -action "POST" -resType "dbs" -connectionKey $accountKey
$body = "{ ""id"": ""$dbName"" }";
$response = Invoke-RestMethod -Uri $uri -Method "Post" -Headers $headers -ContentType "application/json" -Body $body;


# Create the "agencies" collection in the database
Write-Host " - Creating DocumentDB collection 'agencies'";
$resourceId = "dbs/" + $dbName;
$uri = $baseUri + $resourceId + "/colls";
$headers = BuildHeaders -action "POST" -resType "colls" -resourceId $resourceId -connectionKey $accountKey
$body = "{ ""id"": ""agencies"" }";
$response = Invoke-RestMethod -Uri $uri -Method "Post" -Headers $headers -ContentType "application/json" -Body $body;

# Create the "documents" collection in the database
Write-Host " - Creating DocumentDB collection 'documents'";
$body = "{ ""id"": ""documents"" }";
$response = Invoke-RestMethod -Uri $uri -Method "Post" -Headers $headers -ContentType "application/json" -Body $body;
