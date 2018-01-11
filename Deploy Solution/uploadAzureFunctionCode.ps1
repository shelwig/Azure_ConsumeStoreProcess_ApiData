<#
 .SYNOPSIS
    Creates Azure Functions and uploads the code for the them

 .DESCRIPTION
    Creates Azure Functions and uploads the code for the them

 .PARAMETER resourceGroupName
    The resource group where the Azure Functions will live

 .PARAMETER functionsSiteName
    The name of the Azure Functions site in which the functions should be installed
#>

param(
 [Parameter(Mandatory=$True)]
 [string]
 $resourceGroupName,

 [Parameter(Mandatory=$True)]
 [string]
 $functionsSiteName
)

Write-Host "Uploading Azure Function source code..."

$queueNamePrefix = $resourceGroupName.ToLower();
$subdirectory = "Azure Functions";
$functionNames = Get-ChildItem "$subdirectory\*.cs" | ForEach-Object {$_.BaseName};
$functions = @{};

# Loop through all of the .CS files in the specified subdirectory
# Also read in the corresponding JSON file with property information
foreach ($functionName in $functionNames) {
	$codeFileName = "$subdirectory\$functionName.cs";
	$propertiesFileName = "$subdirectory\$functionName.properties.json";

	$codeFileContent = "$(Get-Content -Path $codeFileName -Raw)";
	$propertiesFileContent = "$(Get-Content -Path $propertiesFileName -Raw)";

	$functionContent = @{
		code = $codeFileContent
		properties = ConvertFrom-Json $propertiesFileContent
	};

	$functions[$functionName] = $functionContent;
}

# Some of the functions' properties need to be customized for each individual installation of this solution.
# This set of statements will change the appropriate values in the function collection
($functions["GetAgencies"].properties.bindings | Where-Object -Property "name" -eq "outputQueue").queueName = $queueNamePrefix + "-agency";

($functions["SaveAgencyToDatabase"].properties.bindings | Where-Object -Property "name" -eq "queueItem").queueName = $queueNamePrefix + "-agency";
($functions["SaveAgencyToDatabase"].properties.bindings | Where-Object -Property "name" -eq "outputDocument").databaseName = $resourceGroupName + "-db";
($functions["SaveAgencyToDatabase"].properties.bindings | Where-Object -Property "name" -eq "outputDocument").connection = $resourceGroupName + "-db";

($functions["GetDocuments"].properties.bindings | Where-Object -Property "name" -eq "outputQueue").queueName = $queueNamePrefix + "-doc-add";
($functions["GetDocuments"].properties.bindings | Where-Object -Property "name" -eq "existingIds").databaseName = $resourceGroupName + "-db";
($functions["GetDocuments"].properties.bindings | Where-Object -Property "name" -eq "existingIds").connection = $resourceGroupName + "-db";

($functions["SaveDocumentToDatabase"].properties.bindings | Where-Object -Property "name" -eq "queueItem").queueName = $queueNamePrefix + "-doc-add";
($functions["SaveDocumentToDatabase"].properties.bindings | Where-Object -Property "name" -eq "outputDocument").databaseName = $resourceGroupName + "-db";
($functions["SaveDocumentToDatabase"].properties.bindings | Where-Object -Property "name" -eq "outputDocument").connection = $resourceGroupName + "-db";
($functions["SaveDocumentToDatabase"].properties.bindings | Where-Object -Property "name" -eq '$return').queueName = $queueNamePrefix + "-doc-ai";

($functions["AnalyzeDocument"].properties.bindings | Where-Object -Property "name" -eq "queueItem").queueName = $queueNamePrefix + "-doc-ai";
($functions["AnalyzeDocument"].properties.bindings | Where-Object -Property "name" -eq "inputDocument").databaseName = $resourceGroupName + "-db";
($functions["AnalyzeDocument"].properties.bindings | Where-Object -Property "name" -eq "inputDocument").connection = $resourceGroupName + "-db";
($functions["AnalyzeDocument"].properties.bindings | Where-Object -Property "name" -eq '$return').queueName = $queueNamePrefix + "-doc-score-category";

($functions["ScoreDocumentCategory"].properties.bindings | Where-Object -Property "name" -eq "queueItem").queueName = $queueNamePrefix + "-doc-score-category";
($functions["ScoreDocumentCategory"].properties.bindings | Where-Object -Property "name" -eq "inputDocument").databaseName = $resourceGroupName + "-db";
($functions["ScoreDocumentCategory"].properties.bindings | Where-Object -Property "name" -eq "inputDocument").connection = $resourceGroupName + "-db";

# Now that we have the source code loaded up and all of the properties set correctly, let's upload the functions to Azure
foreach ($functionName in $functionNames) {
	$resourceName = $functionsSiteName + "/" + $functionName;

	$payload = @{
		config = $functions[$functionName].properties
		files = @{
			"run.csx" = $functions[$functionName].code
		}
	};

	Write-Host "- Uploading function ""$functionName"""
	$functionResource = New-AzureRmResource -ResourceGroupName $resourceGroupName -ResourceType Microsoft.Web/sites/functions -ResourceName $resourceName -PropertyObject $payload -ApiVersion 2016-08-01 -Force
}
