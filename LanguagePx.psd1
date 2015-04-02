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

@{
      ModuleToProcess = 'LanguagePx.psm1'

        ModuleVersion = '0.9.0.0'

                 GUID = '49c5d110-a472-4aea-aa0e-7b4ca012e60f'

               Author = 'Kirk Munro'

          CompanyName = 'Poshoholic Studios'

            Copyright = 'Copyright 2015 Kirk Munro'

          Description = 'DESCRIPTION'

    PowerShellVersion = '4.0'

        NestedModules = @(
                        'LanguagePx.dll'
                        'SnippetPx'
                        )

      CmdletsToExport = @(
                        'Invoke-Keyword'
                        #'New-Keyword'
                        'New-DomainSpecificLanguage'
                        'Register-DslKeywordEvent'
                        'Remove-DomainSpecificLanguage'
                        )

             FileList = @(
                        'LanguagePx.dll'
                        'LanguagePx.psd1'
                        'LanguagePx.psm1'
                        'LICENSE'
                        'NOTICE'
                        'en-us\LanguagePx.dll-Help.xml'
                        'scripts\Export-BinaryModule.ps1'
                        )

          PrivateData = @{
                            PSData = @{
                                Tags = 'dsv dsl dynamic keyword domain specific modeling markup vocabulary language extension'
                                LicenseUri = 'http://apache.org/licenses/LICENSE-2.0.txt'
                                ProjectUri = 'https://github.com/KirkMunro/LanguagePx'
                                IconUri = ''
                                ReleaseNotes = ''
                            }
                        }
}