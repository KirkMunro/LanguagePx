<#############################################################################
DESCRIPTION

Copyright 2015 Kirk Munro

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
#############################################################################>

# Export the cmdlets that are defined in the nested module
Export-ModuleMember -Cmdlet Invoke-Keyword,New-DomainSpecificLanguage,Register-DslKeywordEvent,Remove-DomainSpecificLanguage

# Define aliases for cmdlets exported from the nested module
foreach ($alias in 'dsl','dsv') {
    if (-not (Get-Alias -Name $alias -ErrorAction Ignore)) {
        New-Alias -Name $alias -Value New-DomainSpecificLanguage
        Export-ModuleMember -Alias $alias
    }
}