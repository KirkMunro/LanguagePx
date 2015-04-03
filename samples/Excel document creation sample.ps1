# This example is using the first release of Doug Finke's ImportExcel module.
# You can download that module here: https://github.com/dfinke/ImportExcel

# The pipeline approach to creating an Excel document:
Get-Process | Export-Excel -Path "$([Environment]::GetFolderPath('MyDocuments'))\test.xlsx" -Force -Show -AutoFitColumns -IncludePivotTable -IncludePivotChart -PivotRows Name -PivotData WorkingSet64

# First, let's switch to splatting:
$params = @{
    Path = "$([Environment]::GetFolderPath('MyDocuments'))\test.xlsx"
    Force = $true
    Show = $true
    AutoFitColumns = $true
    IncludePivotTable = $true
    IncludePivotChart = $true
    PivotRows = 'Name'
    PivotData = 'WorkingSet64'
}
Get-Process | Export-Excel @params

# Splatting is easier but if a lot of functionality is added, that function won't
# be very easy to use. This is where a DSL can handle the job more easily because
# of the declarative nature.
New-DomainSpecificLanguage -Name ExcelPx -Syntax {
    ExcelDocMagic Name {
        Properties {
            boolean Overwrite
            boolean Show
            boolean AutoSize
        }
        PivotData {
            string RowPropertyName
            string RowAggregateValue
            boolean IncludeChart
        }
    }
}

Register-DslKeywordEvent -DslName ExcelPx -KeywordPath ExcelDocMagic -EventName OnInvoked -Action {
    $params = @{
        Path = $_.Name
        Force = $_.Properties.Overwrite
        Show = $_.Properties.Show
        AutoFitColumns = $_.Properties.AutoSize
        IncludePivotTable = $_.Contains('PivotData')
        IncludePivotChart = $_.Contains('PivotData') -and $_.PivotData.IncludeChart
        PivotRows = $_.PivotData.RowPropertyName
        PivotData = $_.PivotData.RowAggregateValue
    }
    Get-Process | Export-Excel @params    
}

# Now with that DSL defined, we can create Excel documents like this:
ExcelDocMagic "$([Environment]::GetFolderPath('MyDocuments'))\test.xlsx" {
    Properties {
        Overwrite = $true
        Show = $true
        AutoSize = $true
    }
    PivotData {
        RowPropertyName = 'Name'
        RowAggregateValue = 'WorkingSet64'
        IncludeChart = $true
    }
}